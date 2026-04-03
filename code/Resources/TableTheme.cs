using System.Collections.Generic;

namespace MahjongRising.code.Resources;

/// <summary>
/// 麻将桌主题配置。
/// 定义一整套桌面场景的视觉资源：桌子模型、毡布、灯光、背景等。
/// Mod 可注册全新的桌面主题。
/// </summary>
public class TableTheme
{
    /// <summary>主题唯一 ID。</summary>
    public string ThemeId { get; init; } = "";

    /// <summary>主题显示名。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>主题描述。</summary>
    public string Description { get; set; } = "";

    /// <summary>主题预览缩略图路径。</summary>
    public string ThumbnailPath { get; set; } = "";

    // ── 3D 桌面场景 ──

    /// <summary>麻将桌 3D 模型路径（.glb/.tscn）。</summary>
    public string TableModelPath { get; set; } = "";

    /// <summary>桌面毡布/桌面材质贴图。</summary>
    public string FeltTexturePath { get; set; } = "";

    /// <summary>桌面法线贴图（可选，布料凹凸）。</summary>
    public string? FeltNormalMapPath { get; set; }

    /// <summary>桌面 ORM 贴图（可选，遮蔽/粗糙/金属度）。</summary>
    public string? FeltOrmMapPath { get; set; }

    /// <summary>桌框/边缘材质贴图。</summary>
    public string? TableEdgeTexturePath { get; set; }

    // ── 场景环境 ──

    /// <summary>环境光 HDRI / Environment 路径（.tres/.hdr）。</summary>
    public string? EnvironmentPath { get; set; }

    /// <summary>场景背景模型路径（房间/室外景观等，可选）。</summary>
    public string? BackgroundScenePath { get; set; }

    /// <summary>主光源配置预设路径（.tres）。</summary>
    public string? LightingPresetPath { get; set; }

    // ── 桌上物件 ──

    /// <summary>骰子模型路径。</summary>
    public string? DiceModelPath { get; set; }

    /// <summary>点棒 3D 模型路径。</summary>
    public string? PointStickModelPath { get; set; }

    /// <summary>立直棒 3D 模型路径。</summary>
    public string? RiichiStickModelPath { get; set; }

    /// <summary>风向指示牌模型路径。</summary>
    public string? WindIndicatorModelPath { get; set; }

    // ── 牌相关覆盖 ──

    /// <summary>该主题下通用牌模型覆盖（如果主题用特殊形状的牌）。</summary>
    public string? TileModelOverridePath { get; set; }

    /// <summary>该主题下的默认牌背贴图。</summary>
    public string DefaultTileBackTexturePath { get; set; } = "";

    // ── 2D UI 覆盖 ──

    /// <summary>该主题的 UI 面板背景。</summary>
    public string? UiPanelBackgroundPath { get; set; }

    /// <summary>该主题的计分板背景。</summary>
    public string? ScoreboardBackgroundPath { get; set; }

    // ── 音效/音乐 ──

    /// <summary>该主题的背景音乐路径。</summary>
    public string? BgmPath { get; set; }

    /// <summary>环境音效（如室内空调声、室外虫鸣等）。</summary>
    public string? AmbientSfxPath { get; set; }

    /// <summary>洗牌音效。</summary>
    public string? ShuffleSfxPath { get; set; }

    /// <summary>掷骰音效。</summary>
    public string? DiceRollSfxPath { get; set; }

    // ── Mod 附加 ──

    public Dictionary<string, string> ExtraResources { get; init; } = new();

    /// <summary>创建默认内置主题。</summary>
    public static TableTheme CreateDefault()
    {
        string b = ResourcePathConstants.BuiltinRoot;
        return new TableTheme
        {
            ThemeId = "builtin.classic",
            DisplayName = "经典绿桌",
            Description = "传统日式麻将桌，深绿毡布",
            ThumbnailPath = $"{b}/table/thumbnails/classic.png",
            TableModelPath = $"{b}/table/models/table_classic.glb",
            FeltTexturePath = $"{b}/table/textures/felt_green.png",
            FeltNormalMapPath = $"{b}/table/textures/felt_green_normal.png",
            EnvironmentPath = $"{b}/table/env/room_warm.tres",
            DiceModelPath = $"{b}/table/models/dice.glb",
            PointStickModelPath = $"{b}/table/models/point_stick.glb",
            RiichiStickModelPath = $"{b}/table/models/riichi_stick.glb",
            WindIndicatorModelPath = $"{b}/table/models/wind_indicator.glb",
            DefaultTileBackTexturePath = $"{b}/tiles/backs/back_classic.png",
            BgmPath = $"{ResourcePathConstants.Music}/bgm_classic.ogg",
            AmbientSfxPath = $"{ResourcePathConstants.Sfx}/ambient_room.ogg",
            ShuffleSfxPath = $"{ResourcePathConstants.Sfx}/shuffle.ogg",
            DiceRollSfxPath = $"{ResourcePathConstants.Sfx}/dice_roll.ogg"
        };
    }
}
