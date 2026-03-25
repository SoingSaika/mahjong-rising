using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.Game.Rpc;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.State;
using MahjongRising.code.Session;

namespace MahjongRising.code.AI;

/// <summary>
/// AI 玩家适配器。
/// 监听游戏状态变化，在 AI 玩家需要决策时调用 IAiPlayer 并提交结果。
///
/// AI 不通过 RPC 通信（本地直接调用），但遵循与人类玩家完全相同的验证流程：
///   1. 监听到轮到 AI → 调用 IAiPlayer.DecideDiscard()
///   2. 构造 DiscardRequestDto → 直接调用 RpcManager 的服务器逻辑
///   3. 服务器正常验证和广播
///
/// 这保证了 AI 不会绕过规则，且人类玩家看到的 AI 行为与远程玩家完全一致。
///
/// AI 有一个小延迟（0.3~1.5 秒），避免瞬间出牌影响体验。
/// </summary>
public partial class AiPlayerAdapter : Node
{
    /// <summary>AI peer ID 基数。AI seat i 的 peerId = AiPeerBase + i。</summary>
    public const long AiPeerBase = 100000;

    private Dictionary<int, IAiPlayer> _aiPlayers = new();
    private PlayerActionRpcManager _rpcManager = null!;
    private MahjongGameState _gameState = null!;
    private MahjongRuleEngine _engine = null!;

    // ── 决策队列（延迟执行，模拟思考时间） ──
    private readonly Queue<PendingDecision> _pendingDecisions = new();
    private double _decisionTimer;

    // ── 状态跟踪（避免重复决策） ──
    private int _lastProcessedTurn = -1;
    private string _lastProcessedPhase = "";

    private readonly GameSession _session;

    public AiPlayerAdapter(GameSession session)
    {
        _session = session;
    }

    public void Initialize(
        Dictionary<int, IAiPlayer> aiPlayers,
        PlayerActionRpcManager rpcManager,
        MahjongGameState gameState,
        MahjongRuleEngine engine)
    {
        _aiPlayers = aiPlayers;
        _rpcManager = rpcManager;
        _gameState = gameState;
        _engine = engine;
    }

    public void Process(double delta)
    {
        if (_gameState == null || _aiPlayers.Count == 0) return;

        // 处理延迟决策队列
        if (_pendingDecisions.Count > 0)
        {
            _decisionTimer -= delta;
            if (_decisionTimer <= 0)
            {
                var decision = _pendingDecisions.Dequeue();
                ExecuteDecision(decision);
                _decisionTimer = 0;
            }
            return; // 等待当前决策执行完毕
        }

        // 检查是否有 AI 需要做决策
        CheckForAiDecisions();
    }

    // ═══════════════════════════════════
    // 决策检查
    // ═══════════════════════════════════

    private void CheckForAiDecisions()
    {
        string currentPhase = _gameState.Phase.ToString();
        int currentTurn = _gameState.TurnCount;

        // 避免同一状态重复触发
        string stateKey = $"{currentTurn}_{currentPhase}_{_gameState.CurrentTurnSeat}";
        if (stateKey == _lastProcessedPhase) return;

        int currentSeat = _gameState.CurrentTurnSeat;

        switch (_gameState.Phase)
        {
            // 自摸动作阶段：检查当前玩家是否是 AI
            case Game.Rules.TurnPhase.SelfActionPhase:
                if (_aiPlayers.TryGetValue(currentSeat, out var selfAi))
                {
                    _lastProcessedPhase = stateKey;
                    ScheduleSelfAction(selfAi);
                }
                break;

            // 弃牌阶段：当前玩家是 AI 则弃牌
            case Game.Rules.TurnPhase.DiscardPhase:
                if (_aiPlayers.TryGetValue(currentSeat, out var discardAi))
                {
                    _lastProcessedPhase = stateKey;
                    ScheduleDiscard(discardAi);
                }
                break;

            // 反应阶段：检查所有 AI 是否需要做反应
            case Game.Rules.TurnPhase.ReactionPhase:
                if (_gameState.ReactionWindow.IsOpen)
                {
                    _lastProcessedPhase = stateKey;
                    ScheduleReactions();
                }
                break;
        }
    }

    // ═══════════════════════════════════
    // 决策调度（带延迟）
    // ═══════════════════════════════════

    private void ScheduleDiscard(IAiPlayer ai)
    {
        var self = _gameState.GetPlayer(ai.Seat);
        var decision = ai.DecideDiscard(_gameState, self);

        Enqueue(new PendingDecision
        {
            Type = DecisionType.Discard,
            Seat = ai.Seat,
            TileInstanceId = decision.TileInstanceId.ToString(),
            IsRiichi = decision.IsRiichi
        }, ThinkDelay());
    }

    private void ScheduleSelfAction(IAiPlayer ai)
    {
        var self = _gameState.GetPlayer(ai.Seat);
        var lastDrawn = self.Hand.LastOrDefault();
        if (lastDrawn == null) return;

        // 获取可用自摸动作
        var available = _engine.GetAvailableSelfActions(_gameState, self, lastDrawn);
        if (!available.HasAny)
        {
            // 没有自摸动作，直接弃牌
            ScheduleDiscard(ai);
            return;
        }

        var actionsDto = RpcSerializer.ToDto(available);
        var decision = ai.DecideSelfAction(_gameState, self, actionsDto);

        if (decision == null)
        {
            // AI 选择不执行自摸动作 → 弃牌
            _gameState.Phase = Game.Rules.TurnPhase.DiscardPhase;
            ScheduleDiscard(ai);
        }
        else
        {
            Enqueue(new PendingDecision
            {
                Type = DecisionType.SelfAction,
                Seat = ai.Seat,
                ActionType = decision.ActionType,
                OptionId = decision.OptionId
            }, ThinkDelay());
        }
    }

    private void ScheduleReactions()
    {
        var window = _gameState.ReactionWindow;
        int sourceSeat = window.SourceSeat ?? -1;

        foreach (var (seat, ai) in _aiPlayers)
        {
            if (seat == sourceSeat) continue;
            if (window.Responses.ContainsKey(seat) && window.Responses[seat] != null) continue;

            var self = _gameState.GetPlayer(seat);
            var sourceTile = _engine.FindTileById(_gameState, window.SourceTileInstanceId!.Value);
            if (sourceTile == null) { SubmitPass(seat); continue; }

            var available = _engine.GetAvailableReactions(_gameState, self, sourceTile);
            if (!available.HasAny) { SubmitPass(seat); continue; }

            var actionsDto = RpcSerializer.ToDto(available);
            var decision = ai.DecideReaction(_gameState, self, actionsDto);

            Enqueue(new PendingDecision
            {
                Type = DecisionType.Reaction,
                Seat = seat,
                ActionType = decision.ActionType,
                OptionId = decision.OptionId
            }, ThinkDelay(0.2f, 0.8f));
        }
    }

    // ═══════════════════════════════════
    // 执行（直接调用服务器逻辑，不走 RPC 网络）
    // ═══════════════════════════════════

    private void ExecuteDecision(PendingDecision d)
    {
        switch (d.Type)
        {
            case DecisionType.Discard:
                var discardJson = RpcSerializer.Serialize(new DiscardRequestDto
                {
                    TileInstanceId = d.TileInstanceId ?? "",
                    IsRiichi = d.IsRiichi
                });
                // 模拟客户端 RPC 调用（直接传入，跳过网络）
                _rpcManager.HandleAiDiscard(d.Seat, discardJson);
                break;

            case DecisionType.SelfAction:
                var selfJson = RpcSerializer.Serialize(new SelfActionRequestDto
                {
                    ActionType = d.ActionType ?? "",
                    OptionId = d.OptionId ?? ""
                });
                _rpcManager.HandleAiSelfAction(d.Seat, selfJson);
                break;

            case DecisionType.Reaction:
                if (d.ActionType == "pass")
                {
                    SubmitPass(d.Seat);
                }
                else
                {
                    var reactJson = RpcSerializer.Serialize(new ReactionRequestDto
                    {
                        ActionType = d.ActionType ?? "",
                        OptionId = d.OptionId ?? ""
                    });
                    _rpcManager.HandleAiReaction(d.Seat, reactJson);
                }
                break;
        }
    }

    private void SubmitPass(int seat)
    {
        _gameState.ReactionWindow.SubmitPass(seat);
    }

    // ═══════════════════════════════════
    // 工具
    // ═══════════════════════════════════

    private void Enqueue(PendingDecision decision, float delay)
    {
        _pendingDecisions.Enqueue(decision);
        if (_decisionTimer <= 0) _decisionTimer = delay;
    }

    private float ThinkDelay(float min = 0.5f, float max = 1.5f)
    {
        return (float)(min + new System.Random().NextDouble() * (max - min));
    }

    private enum DecisionType { Discard, SelfAction, Reaction }

    private class PendingDecision
    {
        public DecisionType Type { get; init; }
        public int Seat { get; init; }
        public string? TileInstanceId { get; init; }
        public string? ActionType { get; init; }
        public string? OptionId { get; init; }
        public bool IsRiichi { get; init; }
    }
}
