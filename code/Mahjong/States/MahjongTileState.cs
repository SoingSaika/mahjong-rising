using System;
using System.Collections.Generic;
using MahjongRising.code.Game;
using MahjongRising.code.Mahjong.Tiles;

namespace MahjongRising.code.Mahjong.States;

public class MahjongTileState
{
    public Guid InstanceId { get; init; } = Guid.NewGuid();

    // 牌定义
    public MahjongTile Tile { get; init; }

    // 当前归属玩家，-1 表示无归属/公共区域
    public int OwnerSeat { get; set; } = -1;

    // 当前所在区域
    public MahjongTileZone Zone { get; set; } = MahjongTileZone.Wall;

    // 是否正面朝上
    public bool IsFaceUp { get; set; }

    // 是否被锁定（不能被弃置/移动）
    public bool IsLocked { get; set; }

    // 动态标签，局内临时效果放这里
    public HashSet<string> RuntimeTags { get; } = new();

    // 各种计数器，例如“剩余触发次数”“污染层数”
    public Dictionary<string, int> Counters { get; } = new();

    public MahjongTileState(MahjongTile tile)
    {
        Tile = tile;
    }
}