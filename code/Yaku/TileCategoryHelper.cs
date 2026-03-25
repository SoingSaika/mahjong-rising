namespace MahjongRising.code.Yaku;

/// <summary>
/// 牌分类名标准化工具。
/// 支持中文别名 → 英文标准名映射。
/// </summary>
public static class TileCategoryHelper
{
    public static string Normalize(string cat) => cat switch
    {
        "万" => "man", "筒" => "pin", "条" => "sou",
        "风" => "wind", "三元" => "dragon",
        _ => cat
    };

    public static bool IsNumber(string cat)
        => cat is "man" or "pin" or "sou";

    public static bool IsHonor(string cat)
        => cat is "wind" or "dragon";

    public static bool IsTerminal(string cat, int rank)
        => IsNumber(cat) && (rank == 1 || rank == 9);
}
