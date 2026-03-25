using System.Threading.Tasks;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.State;

namespace MahjongRising.code.Player.Actions;

public interface IPlayerActionHandler<in TAction> where TAction : PlayerAction
{
    Task<MahjongRuleResult> Execute(
        MahjongGameState gameState,
        TAction action,
        MahjongRuleEngine engine);
}