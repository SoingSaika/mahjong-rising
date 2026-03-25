using System;
using System.Threading.Tasks;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.State;

namespace MahjongRising.code.Player.Actions;

public interface IPlayerActionHandlerAdapter
{
    Type ActionType { get; }

    Task<MahjongRuleResult> Execute(
        MahjongGameState gameState,
        PlayerAction action,
        MahjongRuleEngine engine);
}