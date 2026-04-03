using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.State;
using MahjongRising.code.Session;
using MahjongRising.code.Session.Rpc;
using PlayerActionRpcManager = MahjongRising.code.Session.Rpc.PlayerActionRpcManager;

namespace MahjongRising.code.AI;

public partial class AiPlayerAdapter : Node
{
    public const long AiPeerBase = 100000;

    private readonly Dictionary<int, IAiPlayer> _ai;
    private readonly PlayerActionRpcManager _rpc;
    private readonly MahjongGameState _gs;
    private readonly MahjongRuleEngine _eng;
    private readonly GameSession _session;

    private readonly Queue<(double delay, System.Action action)> _queue = new();
    private double _timer;
    private string _lastState = "";

    public AiPlayerAdapter(GameSession session, Dictionary<int, IAiPlayer> ai,
        PlayerActionRpcManager rpc, MahjongGameState gs, MahjongRuleEngine eng)
    {
        _session = session; _ai = ai; _rpc = rpc; _gs = gs; _eng = eng;
    }

    public new void Process(double delta)
    {
        if (_queue.Count > 0)
        {
            _timer -= delta;
            if (_timer <= 0)
            {
                var (_, act) = _queue.Dequeue();
                act();
                // 给下一个排队项设置独立延迟
                _timer = _queue.Count > 0 ? _queue.Peek().delay : 0;
            }
            return;
        }
        CheckDecisions();
    }

    private void CheckDecisions()
    {
        string key = $"{_gs.TurnCount}_{_gs.Phase}_{_gs.CurrentTurnSeat}";
        if (key == _lastState) return;

        int seat = _gs.CurrentTurnSeat;

        switch (_gs.Phase)
        {
            case Game.Rules.TurnPhase.SelfActionPhase when _ai.ContainsKey(seat):
                _lastState = key;
                var lastTile = _gs.GetPlayer(seat).Hand.LastOrDefault();
                if (lastTile != null)
                {
                    var sa = _eng.GetAvailableSelfActions(_gs, _gs.GetPlayer(seat), lastTile);
                    if (sa.HasAny)
                    {
                        var dec = _ai[seat].DecideSelfAction(_gs, _gs.GetPlayer(seat), RpcSerializer.ToDto(sa));
                        if (dec != null) { Enqueue(0.6, () => _rpc.HandleAiSelfAction(seat, RpcSerializer.Serialize(new SelfActionRequestDto { ActionType = dec.ActionType, OptionId = dec.OptionId }))); return; }
                    }
                }
                // 无自摸动作或 AI 选择不执行 → 直接弃牌（server 接受 SelfActionPhase 中的弃牌）
                ScheduleDiscard(seat);
                break;

            case Game.Rules.TurnPhase.DiscardPhase when _ai.ContainsKey(seat):
                _lastState = key;
                ScheduleDiscard(seat);
                break;

            case Game.Rules.TurnPhase.ReactionPhase when _gs.ReactionWindow.IsOpen:
                _lastState = key;
                ScheduleReactions();
                break;
        }
    }

    private void ScheduleDiscard(int seat)
    {
        var dec = _ai[seat].DecideDiscard(_gs, _gs.GetPlayer(seat));
        Enqueue(0.5, () => _rpc.HandleAiDiscard(seat, RpcSerializer.Serialize(
            new DiscardRequestDto { TileInstanceId = dec.TileInstanceId.ToString(), IsRiichi = dec.IsRiichi })));
    }

    private void ScheduleReactions()
    {
        var w = _gs.ReactionWindow;
        int src = w.SourceSeat ?? -1;
        foreach (var (seat, ai) in _ai)
        {
            if (seat == src) continue;
            if (w.Responses.ContainsKey(seat) && w.Responses[seat] != null) continue;

            var tile = _eng.FindTileById(_gs, w.SourceTileInstanceId!.Value);
            if (tile == null) { w.SubmitPass(seat); continue; }

            var avail = _eng.GetAvailableReactions(_gs, _gs.GetPlayer(seat), tile);
            if (!avail.HasAny) { w.SubmitPass(seat); continue; }

            var dec = ai.DecideReaction(_gs, _gs.GetPlayer(seat), RpcSerializer.ToDto(avail));
            Enqueue(0.3, () =>
            {
                if (dec.ActionType == "pass") _rpc.HandleAiReaction(seat, RpcSerializer.Serialize(new ReactionRequestDto { ActionType = "pass" }));
                else _rpc.HandleAiReaction(seat, RpcSerializer.Serialize(new ReactionRequestDto { ActionType = dec.ActionType, OptionId = dec.OptionId }));
            });
        }
    }

    private void Enqueue(double delay, System.Action action)
    {
        _queue.Enqueue((delay, action));
        if (_timer <= 0) _timer = delay;
    }
}