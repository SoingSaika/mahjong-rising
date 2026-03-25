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
/// 一局游戏的会话管理。
/// 由 RoomManager 创建，拥有 GameState、RuleEngine、RpcManager、AI 玩家。
///
/// 生命周期：
///   RoomManager.CreateRoom(config)
///     → new GameSession(config, bootstrap)
///     → session.AddHumanPlayer(peerId, seat)  // 多人时多次调用
///     → session.Start()                       // 配牌、庄家摸牌
///     → ...（游戏进行中）...
///     → session.Dispose()                     // 结束，清理资源
/// </summary>
public partial class GameSession : Node
{
    private readonly RoomConfig _config;
    private readonly GameBootstrap _bootstrap;

    // ── 核心状态 ──
    public MahjongGameState GameState { get; private set; } = null!;
    public MahjongRuleEngine Engine { get; private set; } = null!;
    public PlayerActionRpcManager RpcManager { get; private set; } = null!;

    // ── AI ──
    private readonly Dictionary<int, IAiPlayer> _aiPlayers = new();
    private AiPlayerAdapter? _aiAdapter;

    // ── 回调（通知 UI 层） ──
    [Signal] public delegate void RoundStartedEventHandler();
    [Signal] public delegate void RoundEndedEventHandler(string reason);
    [Signal] public delegate void GameFinishedEventHandler();

    public GameSession(RoomConfig config, GameBootstrap bootstrap)
    {
        _config = config;
        _bootstrap = bootstrap;
    }

    public override void _Ready()
    {
        BuildEngine();
        BuildGameState();
        BuildRpcManager();
        BuildAiPlayers();
    }

    // ═══════════════════════════════════
    // 构建
    // ═══════════════════════════════════

    private void BuildEngine()
    {
        Engine = new MahjongRuleEngine(
            _bootstrap.ActionHandlers,
            new DefaultActionPriorityResolver(),
            _bootstrap.Validators);
    }

    private void BuildGameState()
    {
        GameState = new MahjongGameState();

        for (int i = 0; i < _config.PlayerCount; i++)
        {
            var player = new PlayerState(i)
            {
                SeatWind = i,
                Score = 25000
            };
            GameState.Players.Add(player);
        }

        GameState.DealerSeat = 0;
        GameState.RoundWind = 0;
        GameState.ReactionWindow.TimeoutSeconds = _config.ReactionTimeoutSeconds;
    }

    private void BuildRpcManager()
    {
        RpcManager = new PlayerActionRpcManager();
        AddChild(RpcManager);
        // Initialize 在 Start() 中调用，因为需要先分配 PeerId
    }

    private void BuildAiPlayers()
    {
        _aiAdapter = new AiPlayerAdapter(this);
        AddChild(_aiAdapter);

        // 确定哪些座位是 AI
        var humanSeats = GameState.Players
            .Where(p => p.PeerId > 0)
            .Select(p => p.Seat)
            .ToHashSet();

        for (int i = 0; i < _config.PlayerCount; i++)
        {
            if (humanSeats.Contains(i)) continue;

            IAiPlayer ai = _config.AiDifficulty switch
            {
                "easy" => new BasicAiPlayer(i, AiStyle.Defensive),
                "hard" => new BasicAiPlayer(i, AiStyle.Aggressive),
                _ => new BasicAiPlayer(i, AiStyle.Balanced)
            };

            _aiPlayers[i] = ai;
            GameState.GetPlayer(i).PeerId = AiPlayerAdapter.AiPeerBase + i;
        }
    }

    // ═══════════════════════════════════
    // 人类玩家管理
    // ═══════════════════════════════════

    /// <summary>添加一个人类玩家。多人模式下，每个连接的 peer 调用一次。</summary>
    public void AddHumanPlayer(long peerId, int seat, string characterId = "")
    {
        var player = GameState.GetPlayer(seat);
        player.PeerId = peerId;

        // 移除该座位的 AI（如果有）
        _aiPlayers.Remove(seat);
    }

    /// <summary>单人模式：本地玩家总是 seat 0。</summary>
    public void AddLocalPlayer(string characterId = "")
    {
        // 单人模式时 PeerId = 1（Godot 本地服务器的 unique ID）
        AddHumanPlayer(1, 0, characterId);
    }

    // ═══════════════════════════════════
    // 开始 / 结束
    // ═══════════════════════════════════

    /// <summary>开始游戏。配牌 → 庄家摸牌。</summary>
    public void Start()
    {
        // 洗牌
        ShuffleAndDeal();

        // 初始化 RPC（绑定 seat → peerId）
        RpcManager.Initialize(GameState, Engine);

        // 通知 AI 适配器就绪
        _aiAdapter?.Initialize(_aiPlayers, RpcManager, GameState, Engine);

        // 开局
        RpcManager.ServerStartRound();
        EmitSignal(SignalName.RoundStarted);

        GD.Print($"[Session] 游戏开始：{_config.PlayerCount} 人，{_aiPlayers.Count} AI");
    }

    /// <summary>洗牌配牌。</summary>
    private void ShuffleAndDeal()
    {
        // 生成完整牌组
        var allTiles = _bootstrap.Tiles.CreateFullSet();
        var rng = new Random();

        // 洗牌（Fisher-Yates）
        for (int i = allTiles.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (allTiles[i], allTiles[j]) = (allTiles[j], allTiles[i]);
        }

        // 转为 TileState
        var tileStates = allTiles.Select(t => new MahjongTileState(t)).ToList();

        // 分配：14 张王牌 → DeadWall，每人 13 张 → Hand，剩余 → Wall
        int idx = 0;

        // 配牌（每人 13 张）
        for (int round = 0; round < 13; round++)
        {
            for (int seat = 0; seat < _config.PlayerCount; seat++)
            {
                var ts = tileStates[idx++];
                ts.Zone = MahjongTileZone.Hand;
                ts.OwnerSeat = seat;
                GameState.GetPlayer(seat).Hand.Add(ts);
            }
        }

        // 王牌区（14 张）
        for (int i = 0; i < 14 && idx < tileStates.Count; i++)
        {
            var ts = tileStates[idx++];
            ts.Zone = MahjongTileZone.DeadWall;
            GameState.DeadWall.Add(ts);
        }

        // 翻开第一张宝牌指示牌
        if (GameState.DeadWall.Count > 0)
        {
            var doraIndicator = GameState.DeadWall.Last();
            GameState.DeadWall.Remove(doraIndicator);
            doraIndicator.Zone = MahjongTileZone.Reveal;
            doraIndicator.IsFaceUp = true;
            GameState.RevealedTiles.Add(doraIndicator);
        }

        // 剩余 → 牌山
        while (idx < tileStates.Count)
        {
            var ts = tileStates[idx++];
            ts.Zone = MahjongTileZone.Wall;
            GameState.Wall.Add(ts);
        }
    }

    /// <summary>结束会话，清理资源。</summary>
    public void Finish()
    {
        EmitSignal(SignalName.GameFinished);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        // 反应窗口超时
        if (Multiplayer.IsServer() || _config.Mode == "solo")
        {
            RpcManager?.CheckReactionTimeout();
        }

        // AI 思考
        _aiAdapter?.Process(delta);
    }

    // ═══════════════════════════════════
    // AI 查询入口（AiPlayerAdapter 调用）
    // ═══════════════════════════════════

    internal bool IsAiSeat(int seat) => _aiPlayers.ContainsKey(seat);
    internal IAiPlayer? GetAi(int seat) => _aiPlayers.GetValueOrDefault(seat);
}
