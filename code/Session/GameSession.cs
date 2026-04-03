using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.AI;
using MahjongRising.code.Game;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.Actions;
using MahjongRising.code.Player.States;
using MahjongRising.code.Session.Rpc;
using PlayerActionRpcManager = MahjongRising.code.Session.Rpc.PlayerActionRpcManager;

namespace MahjongRising.code.Session;

/// <summary>
/// 一局游戏会话。拥有两个 RPC 子节点：
///   RoomRpcManager      — 房间生命周期（加入/离开/AI/角色/开始/结束）
///   PlayerActionRpcManager — 游戏内操作（摸/弃/吃/碰/杠/胡）
///
/// 两端（host+client）节点树结构完全一致：
///   RoomManager/GameSession/RoomRpc
///   RoomManager/GameSession/GameRpc
/// </summary>
public partial class GameSession : Node
{
    public RoomConfig Config { get; init; } = null!;
    public MahjongGameState GameState { get; private set; } = null!;
    public MahjongRuleEngine Engine { get; private set; } = null!;

    /// <summary>房间 RPC（大厅阶段 + 游戏结束后）。</summary>
    public Rpc.RoomRpcManager RoomRpc { get; private set; } = null!;

    /// <summary>游戏内 RPC。</summary>
    public PlayerActionRpcManager GameRpc { get; private set; } = null!;

    private GameBootstrap _boot = null!;
    private readonly Dictionary<int, IAiPlayer> _aiPlayers = new();
    private AiPlayerAdapter? _aiAdapter;

    [Signal] public delegate void GameStartedEventHandler();
    [Signal] public delegate void GameEndedEventHandler();

    public override void _Ready()
    {
        _boot = GetNode<GameBootstrap>("/root/GameBootstrap");
        Engine = new MahjongRuleEngine(_boot.ActionHandlers, new DefaultActionPriorityResolver(), _boot.Validators);
        GameState = new MahjongGameState();

        // 创建两个 RPC 子节点（名称固定，保证两端路径一致）
        RoomRpc = new Rpc.RoomRpcManager { Name = "RoomRpc" };
        AddChild(RoomRpc);

        GameRpc = new PlayerActionRpcManager { Name = "GameRpc" };
        AddChild(GameRpc);
    }

    // ═══ 座位管理（通过 RoomRpcManager 驱动） ═══

    /// <summary>服务端：设置人类玩家。</summary>
    public void SetHumanPlayer(int seat, long peerId)
    {
        EnsurePlayerExists(seat);
        GameState.GetPlayer(seat).PeerId = peerId;
        _aiPlayers.Remove(seat);
    }

    /// <summary>服务端：设置 AI。</summary>
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

    public void RemovePlayer(int seat)
    {
        if (seat <= 0 || seat >= GameState.Players.Count) return;
        _aiPlayers.Remove(seat);
        GameState.GetPlayer(seat).PeerId = 0;
    }

    public bool IsAiSeat(int seat) => _aiPlayers.ContainsKey(seat);

    // ═══ 开始游戏 ═══

    /// <summary>
    /// 服务端调用。从 RoomRpcManager 读取最终座位状态，配牌开局。
    /// </summary>
    public void StartGame()
    {
        // 从 RoomRpc 同步座位到 GameState
        for (int i = 0; i < Config.PlayerCount; i++)
        {
            EnsurePlayerExists(i);
            var p = GameState.GetPlayer(i);
            p.SeatWind = i;

            var status = RoomRpc.GetStatus(i);
            if (status == SlotStatus.Human)
            {
                p.PeerId = RoomRpc.GetPeerId(i);
                _aiPlayers.Remove(i);
            }
            else if (status == SlotStatus.Ai)
            {
                SetAiPlayer(i, RoomRpc.GetAiDifficulty(i));
            }
            else
            {
                // 空位补 AI
                SetAiPlayer(i, Config.AiDifficulty);
                RoomRpc.ServerSetAi(i, Config.AiDifficulty);
            }
        }

        GameState.DealerSeat = 0;
        GameState.RoundWind = 0;
        GameState.ReactionWindow.TimeoutSeconds = Config.ReactionTimeoutSeconds;

        ShuffleAndDeal();
        GameRpc.Initialize(GameState, Engine, _boot.YakuRules, _boot.Scoring);

        if (_aiPlayers.Count > 0)
        {
            _aiAdapter = new AiPlayerAdapter(this, _aiPlayers, GameRpc, GameState, Engine);
            AddChild(_aiAdapter);
        }

        GameRpc.ServerStartRound();
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
            GameRpc?.CheckReactionTimeout();
            _aiAdapter?.Process(delta);
        }
    }

    public void EndGame() { EmitSignal(SignalName.GameEnded); QueueFree(); }
}