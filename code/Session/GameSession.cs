using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.AI;
using MahjongRising.code.Game;
using MahjongRising.code.Game.Rpc;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.Actions;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Session;

/// <summary>
/// 一局游戏会话。
/// 服务端创建并管理 GameState + RPC + AI。
/// 客户端通过 RpcManager 的事件接收通知。
/// </summary>
public partial class GameSession : Node
{
    public RoomConfig Config { get; init; } = null!;
    public MahjongGameState GameState { get; private set; } = null!;
    public MahjongRuleEngine Engine { get; private set; } = null!;
    public PlayerActionRpcManager Rpc { get; private set; } = null!;

    private GameBootstrap _boot = null!;
    private readonly Dictionary<int, IAiPlayer> _aiPlayers = new();
    private AiPlayerAdapter? _aiAdapter;

    [Signal] public delegate void GameStartedEventHandler();
    [Signal] public delegate void GameEndedEventHandler();

    /// <summary>客户端缓存的初始化数据（RoomLobby 收到后存入，GameBoardUI 读取）。</summary>
    public Game.Rpc.GameInitEventDto? CachedInitData { get; set; }

    public override void _Ready()
    {
        _boot = GetNode<GameBootstrap>("/root/GameBootstrap");
        Engine = new MahjongRuleEngine(_boot.ActionHandlers, new DefaultActionPriorityResolver(), _boot.Validators);
        GameState = new MahjongGameState();
        Rpc = new PlayerActionRpcManager();
        AddChild(Rpc);
    }

    /// <summary>设置人类玩家座位。</summary>
    public void SetHumanPlayer(int seat, long peerId)
    {
        EnsurePlayerExists(seat);
        GameState.GetPlayer(seat).PeerId = peerId;
        _aiPlayers.Remove(seat);
    }

    /// <summary>设置 AI 玩家座位。</summary>
    public void SetAiPlayer(int seat, string difficulty = "normal")
    {
        EnsurePlayerExists(seat);
        var ai = difficulty switch
        {
            "easy" => new BasicAiPlayer(seat, AiStyle.Defensive),
            "hard" => new BasicAiPlayer(seat, AiStyle.Aggressive),
            _ => new BasicAiPlayer(seat, AiStyle.Balanced)
        };
        _aiPlayers[seat] = ai;
        GameState.GetPlayer(seat).PeerId = AiPlayerAdapter.AiPeerBase + seat;
    }

    /// <summary>移除某座位的 AI。</summary>
    public void RemoveAi(int seat) { _aiPlayers.Remove(seat); }

    public bool IsAiSeat(int seat) => _aiPlayers.ContainsKey(seat);

    /// <summary>开始游戏。</summary>
    public void StartGame()
    {
        // 确保所有座位都有人
        for (int i = 0; i < Config.PlayerCount; i++)
        {
            EnsurePlayerExists(i);
            var p = GameState.GetPlayer(i);
            p.SeatWind = i;
            if (p.PeerId == 0 && !_aiPlayers.ContainsKey(i))
                SetAiPlayer(i, Config.AiDifficulty);
        }

        GameState.DealerSeat = 0;
        GameState.RoundWind = 0;
        GameState.ReactionWindow.TimeoutSeconds = Config.ReactionTimeoutSeconds;

        ShuffleAndDeal();
        Rpc.Initialize(GameState, Engine, _boot.YakuRules, _boot.Scoring);

        // AI 适配器
        if (_aiPlayers.Count > 0)
        {
            _aiAdapter = new AiPlayerAdapter(this, _aiPlayers, Rpc, GameState, Engine);
            AddChild(_aiAdapter);
        }

        Rpc.ServerStartRound();
        EmitSignal(SignalName.GameStarted);
    }

    private void ShuffleAndDeal()
    {
        var allTiles = _boot.Tiles.CreateFullSet();
        var rng = new Random();
        for (int i = allTiles.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (allTiles[i], allTiles[j]) = (allTiles[j], allTiles[i]); }

        var states = allTiles.Select(t => new MahjongTileState(t)).ToList();
        int idx = 0;

        for (int round = 0; round < 13; round++)
            for (int seat = 0; seat < Config.PlayerCount; seat++)
            {
                var ts = states[idx++]; ts.Zone = MahjongTileZone.Hand; ts.OwnerSeat = seat;
                GameState.GetPlayer(seat).Hand.Add(ts);
            }

        for (int i = 0; i < 14 && idx < states.Count; i++)
        {
            var ts = states[idx++]; ts.Zone = MahjongTileZone.DeadWall; GameState.DeadWall.Add(ts);
        }

        if (GameState.DeadWall.Count > 0)
        {
            var dora = GameState.DeadWall.Last(); GameState.DeadWall.Remove(dora);
            dora.Zone = MahjongTileZone.Reveal; dora.IsFaceUp = true; GameState.RevealedTiles.Add(dora);
        }

        while (idx < states.Count) { var ts = states[idx++]; ts.Zone = MahjongTileZone.Wall; GameState.Wall.Add(ts); }
    }

    private void EnsurePlayerExists(int seat)
    {
        while (GameState.Players.Count <= seat)
            GameState.Players.Add(new PlayerState(GameState.Players.Count) { Score = 25000 });
    }

    public override void _Process(double delta)
    {
        if (Multiplayer.IsServer() || Config.Mode == "solo")
        {
            Rpc?.CheckReactionTimeout();
            _aiAdapter?.Process(delta);
        }
    }

    public void EndGame() { EmitSignal(SignalName.GameEnded); QueueFree(); }
}