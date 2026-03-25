using System.Collections.Generic;
using System.Linq;

namespace MahjongRising.code.Player.Actions;

public sealed class DefaultActionPriorityResolver : IActionPriorityResolver
{
    public IReadOnlyList<PlayerAction> SortActions(IReadOnlyList<PlayerAction> actions)
    {
        return actions
            .OrderByDescending(a => a.BasePriority)   // 先按 BasePriority
            .ThenBy(a => a.CreatedAtUtc)             // 同优先级按创建时间
            .ToList();
    }
}