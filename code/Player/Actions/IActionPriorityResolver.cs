using System.Collections.Generic;

namespace MahjongRising.code.Player.Actions;

public interface IActionPriorityResolver
{
    IReadOnlyList<PlayerAction> SortActions(
        IReadOnlyList<PlayerAction> actions);
}