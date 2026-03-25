using System.Collections.Generic;
using MahjongRising.code.Mahjong.States;

namespace MahjongRising.code.Player.States;

/// <summary>
/// 一组副露（吃/碰/明杠/暗杠/加杠）。
/// Mod 可扩展 MeldKind 字符串。
/// </summary>
public class MeldGroup
{
    // ── 内置类型常量 ──
    public const string KindChi = "chi";
    public const string KindPeng = "peng";
    public const string KindMinkan = "minkan";     // 明杠
    public const string KindAnkan = "ankan";       // 暗杠
    public const string KindKakan = "kakan";       // 加杠

    /// <summary>副露类型，Mod 可使用自定义字符串。</summary>
    public string Kind { get; init; } = "";

    /// <summary>组成该副露的牌状态列表。</summary>
    public List<MahjongTileState> Tiles { get; init; } = new();

    /// <summary>来源座位（-1 = 自身，如暗杠）。</summary>
    public int SourceSeat { get; init; } = -1;

    /// <summary>是否公开（暗杠为 false）。</summary>
    public bool IsOpen { get; init; } = true;
}
