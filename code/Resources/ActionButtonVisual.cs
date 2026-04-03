using System.Collections.Generic;

namespace MahjongRising.code.Resources;

/// <summary>
/// 玩家动作按钮的视觉资源。
/// 每种动作（吃/碰/杠/胡/立直/跳过/自定义）都有对应的按钮贴图和音效。
/// Mod 可注册全新的动作按钮外观。
/// </summary>
public class ActionButtonVisual
{
    /// <summary>动作类型标识（如 "chi"/"peng"/"gang"/"hu"/"riichi"/"pass"）。</summary>
    public string ActionType { get; init; } = "";

    /// <summary>动作显示名称（本地化键或直接文本）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>动作显示名（日文）。</summary>
    public string DisplayNameJp { get; set; } = "";

    /// <summary>动作显示名（中文）。</summary>
    public string DisplayNameCn { get; set; } = "";

    // ── 按钮贴图 ──

    /// <summary>按钮常态贴图。</summary>
    public string NormalTexturePath { get; set; } = "";

    /// <summary>按钮悬停贴图。</summary>
    public string HoverTexturePath { get; set; } = "";

    /// <summary>按钮按下贴图。</summary>
    public string PressedTexturePath { get; set; } = "";

    /// <summary>按钮禁用态贴图。</summary>
    public string DisabledTexturePath { get; set; } = "";

    /// <summary>按钮图标（不带背景的纯图标，用于紧凑 UI）。</summary>
    public string IconPath { get; set; } = "";

    // ── 动画/特效 ──

    /// <summary>执行动作时的 2D 弹幕/横幅动画路径（如大大的"碰！"字幕）。</summary>
    public string? BannerAnimationPath { get; set; }

    /// <summary>执行动作时的 3D 粒子特效路径。</summary>
    public string? VfxPath { get; set; }

    /// <summary>执行动作时的屏幕着色器效果路径。</summary>
    public string? ScreenEffectShaderPath { get; set; }

    // ── 音效 ──

    /// <summary>按钮点击音效。</summary>
    public string? ClickSfxPath { get; set; }

    /// <summary>动作执行成功时的音效（如碰牌声）。</summary>
    public string? ExecuteSfxPath { get; set; }

    /// <summary>动作宣告语音路径（如角色喊"碰！"的语音）。</summary>
    public string? CallVoicePath { get; set; }

    // ── 按钮布局 ──

    /// <summary>在反应按钮栏中的排序权重（越大越靠左/越突出）。</summary>
    public int DisplayOrder { get; set; }

    /// <summary>按钮颜色主题标签（UI 层根据此值选配色）。</summary>
    public string ColorTheme { get; set; } = "default";

    // ── Mod 附加 ──

    public Dictionary<string, string> ExtraResources { get; init; } = new();

    // ── 内置动作的工厂方法 ──

    public static ActionButtonVisual CreateBuiltin(string actionType)
    {
        string basePath = ResourcePathConstants.ActionButtons;
        return new ActionButtonVisual
        {
            ActionType = actionType,
            DisplayName = GetDefaultDisplayName(actionType),
            DisplayNameJp = GetDefaultDisplayNameJp(actionType),
            DisplayNameCn = GetDefaultDisplayNameCn(actionType),
            NormalTexturePath = $"{basePath}/{actionType}_normal.png",
            HoverTexturePath = $"{basePath}/{actionType}_hover.png",
            PressedTexturePath = $"{basePath}/{actionType}_pressed.png",
            DisabledTexturePath = $"{basePath}/{actionType}_disabled.png",
            IconPath = $"{ResourcePathConstants.UiIcons}/{actionType}.png",
            BannerAnimationPath = $"{ResourcePathConstants.Animations}/banner_{actionType}.tscn",
            ClickSfxPath = $"{ResourcePathConstants.Sfx}/ui_click.ogg",
            ExecuteSfxPath = $"{ResourcePathConstants.Sfx}/{actionType}_call.ogg",
            DisplayOrder = GetDefaultDisplayOrder(actionType),
            ColorTheme = GetDefaultColorTheme(actionType)
        };
    }

    private static string GetDefaultDisplayName(string type) => type switch
    {
        "chi" => "Chii", "peng" => "Pon", "gang" => "Kan",
        "hu" => "Win", "riichi" => "Riichi", "pass" => "Pass",
        "tsumo" => "Tsumo", "ron" => "Ron",
        "ankan" => "Concealed Kan", "kakan" => "Added Kan",
        _ => type
    };

    private static string GetDefaultDisplayNameJp(string type) => type switch
    {
        "chi" => "チー", "peng" => "ポン", "gang" => "カン",
        "hu" => "和了", "riichi" => "リーチ", "pass" => "パス",
        "tsumo" => "ツモ", "ron" => "ロン",
        "ankan" => "暗槓", "kakan" => "加槓",
        _ => type
    };

    private static string GetDefaultDisplayNameCn(string type) => type switch
    {
        "chi" => "吃", "peng" => "碰", "gang" => "杠",
        "hu" => "和", "riichi" => "立直", "pass" => "跳过",
        "tsumo" => "自摸", "ron" => "荣和",
        "ankan" => "暗杠", "kakan" => "加杠",
        _ => type
    };

    private static int GetDefaultDisplayOrder(string type) => type switch
    {
        "hu" or "tsumo" or "ron" => 100,
        "riichi" => 90,
        "gang" or "ankan" or "kakan" => 70,
        "peng" => 60,
        "chi" => 50,
        "pass" => 0,
        _ => 30
    };

    private static string GetDefaultColorTheme(string type) => type switch
    {
        "hu" or "tsumo" or "ron" => "gold",
        "riichi" => "blue",
        "gang" or "ankan" or "kakan" => "red",
        "peng" => "green",
        "chi" => "cyan",
        "pass" => "gray",
        _ => "default"
    };
}
