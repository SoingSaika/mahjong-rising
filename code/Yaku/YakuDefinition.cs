using System.Collections.Generic;

namespace MahjongRising.code.Yaku;

/// <summary>
/// 役种定义（纯数据）。
/// 分离役的"规则判定"和"数据/显示"两个关注点。
/// YakuEvaluator 负责判定，YakuDefinition 负责描述。
/// Mod 注册新役时同时注册 YakuDefinition。
/// </summary>
public class YakuDefinition
{
    /// <summary>役种唯一 ID（如 "riichi"、"tanyao"、"mod.curse_breaker"）。</summary>
    public string YakuId { get; init; } = "";

    // ── 显示名 ──

    /// <summary>日文名。</summary>
    public string NameJp { get; set; } = "";

    /// <summary>日文读音（罗马字）。</summary>
    public string NameRomaji { get; set; } = "";

    /// <summary>中文名。</summary>
    public string NameCn { get; set; } = "";

    /// <summary>英文名。</summary>
    public string NameEn { get; set; } = "";

    // ── 番数 ──

    /// <summary>门前清（闭门）时的番数。</summary>
    public int HanClosed { get; set; }

    /// <summary>副露（鸣牌）时的番数。0 = 不可食下。-1 = 门前限定。</summary>
    public int HanOpen { get; set; }

    /// <summary>是否为役满。</summary>
    public bool IsYakuman { get; set; }

    /// <summary>役满倍数（1 = 单倍役满，2 = 双倍役满）。</summary>
    public int YakumanMultiplier { get; set; } = 1;

    /// <summary>是否门前限定。</summary>
    public bool MenzenOnly => HanOpen < 0;

    // ── 分类 ──

    /// <summary>役种分类标签（"basic" / "yakuhai" / "sequence" / "terminal" / "honor" / "flush" / "yakuman" / "special"）。</summary>
    public string Category { get; set; } = "basic";

    // ── 显示资源 ──

    /// <summary>役名弹出横幅贴图路径。</summary>
    public string? BannerPath { get; set; }

    /// <summary>役种图标路径（用于结算画面）。</summary>
    public string? IconPath { get; set; }

    /// <summary>宣告动画路径（.tscn）。</summary>
    public string? AnnouncementAnimationPath { get; set; }

    /// <summary>宣告音效路径。</summary>
    public string? AnnouncementSfxPath { get; set; }

    // ── 描述 ──

    /// <summary>规则描述（用于帮助/图鉴界面）。</summary>
    public string DescriptionJp { get; set; } = "";
    public string DescriptionCn { get; set; } = "";
    public string DescriptionEn { get; set; } = "";

    // ── Mod 附加 ──

    public Dictionary<string, object> ExtraData { get; init; } = new();
}
