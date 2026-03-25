using System;

namespace MahjongRising.code.Player.Actions;

public abstract class PlayerAction
{
    public int Seat { get; init; }

    public virtual string ActionId => GetType().FullName ?? GetType().Name;

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    // 新增：基础优先级字段，可用于排序
    // 0 默认最低，值越大优先级越高
    public abstract int BasePriority { get; }

    protected PlayerAction(int seat)
    {
        Seat = seat;
    }

    public override string ToString()
    {
        return $"{ActionId} by Seat={Seat} (Priority={BasePriority})";
    }
}