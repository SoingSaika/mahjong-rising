using System.Collections.Generic;
using MahjongRising.code.Mahjong.Actions;

namespace MahjongRising.code.Mahjong.Tiles;

public abstract class MahjongTile(string category, int rank)
{
    public string Category { get; init; } = category;

    public int Rank { get; init; } = rank;

    // 该牌标记，比如 normal / honor / terminal / special
    public List<string> Tags { get; init; } = new();

    // 该牌特殊变体（赤宝、金牌、诅咒等）
    public List<string> Variants { get; init; } = new();

    // 用于存档 / 联机 / UI 显示的稳定编码
    public abstract string TileCode { get; }

    // 默认没有特殊动作
    public virtual MahjongAction? Action => null;
}