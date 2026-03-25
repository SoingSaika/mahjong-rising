using System;
using System.Threading.Tasks;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.State;

namespace MahjongRising.code.Player.Actions;

public sealed class PlayerActionHandlerAdapter<TAction> : IPlayerActionHandlerAdapter
    where TAction : PlayerAction
{
    private readonly IPlayerActionHandler<TAction> _handler;

    public Type ActionType => typeof(TAction);

    public PlayerActionHandlerAdapter(IPlayerActionHandler<TAction> handler)
    {
        _handler = handler;
    }

    public Task<MahjongRuleResult> Execute(
        MahjongGameState gameState,
        PlayerAction action,
        MahjongRuleEngine engine)
    {
        if (action is not TAction typedAction)
            return Task.FromResult(MahjongRuleResult.Fail($"动作类型不匹配：{action.GetType().Name}"));

        return _handler.Execute(gameState, typedAction, engine);
    }
}