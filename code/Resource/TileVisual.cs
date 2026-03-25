using System.Collections.Generic;

namespace MahjongRising.code.Resources;

/// <summary>
/// 单张牌的视觉资源描述。
/// 挂在 MahjongTile 上，由 TileRegistry 注册时一起提供。
/// Mod 可通过注册新的 TileVisual 来添加自定义牌面外观。
///
/// 路径规范：
///   内置牌：res://assets/tiles/faces/man_1.png
///   Mod 牌：user://mods/{modId}/tiles/faces/curse_1.png
///   皮肤覆盖：res://assets/skins/{skinId}/tiles/faces/man_1.png
/// </summary>
public class TileVisual
{
    /// <summary>对应的 TileCode（如 "man_1"、"wind_3"）。</summary>
    public string TileCode { get; init; } = "";

    // ── 3D 资源 ──

    /// <summary>3D 牌模型路径（.glb/.tscn），通常所有牌共用一个模型。</summary>
    public string ModelPath { get; set; } = "";

    /// <summary>牌面贴图路径（.png），渲染到模型正面的 UV 上。</summary>
    public string FaceTexturePath { get; set; } = "";

    /// <summary>牌面法线贴图（可选，用于凹凸效果）。</summary>
    public string? FaceNormalMapPath { get; set; }

    /// <summary>牌背贴图路径（默认使用全局牌背，可单独覆盖）。</summary>
    public string? BackTexturePath { get; set; }

    // ── 2D UI 资源 ──

    /// <summary>手牌 UI 图标路径（较大，用于手牌区显示）。</summary>
    public string IconPath { get; set; } = "";

    /// <summary>小图标路径（用于弃牌区、副露区等缩小显示）。</summary>
    public string SmallIconPath { get; set; } = "";

    /// <summary>灰色/禁用态图标（不能打出时显示）。</summary>
    public string? DisabledIconPath { get; set; }

    // ── 特效 ──

    /// <summary>摸到这张牌时的粒子特效路径（可选）。</summary>
    public string? DrawVfxPath { get; set; }

    /// <summary>弃牌时的粒子特效路径（可选）。</summary>
    public string? DiscardVfxPath { get; set; }

    /// <summary>特殊高亮着色器路径（如赤宝发光效果）。</summary>
    public string? HighlightShaderPath { get; set; }

    // ── 音效 ──

    /// <summary>打出这张牌的音效。</summary>
    public string? DiscardSfxPath { get; set; }

    /// <summary>摸牌音效。</summary>
    public string? DrawSfxPath { get; set; }

    // ── Mod 附加数据 ──

    /// <summary>Mod 可自由扩展的额外资源路径。</summary>
    public Dictionary<string, string> ExtraResources { get; init; } = new();

    /// <summary>
    /// 用默认路径约定创建一个标准牌的 TileVisual。
    /// 遵循 ResourcePathConstants 中的目录结构。
    /// </summary>
    public static TileVisual CreateDefault(string tileCode, string? basePath = null)
    {
        basePath ??= ResourcePathConstants.BuiltinRoot;
        return new TileVisual
        {
            TileCode = tileCode,
            ModelPath = $"{basePath}/tiles/models/tile_standard.glb",
            FaceTexturePath = $"{basePath}/tiles/faces/{tileCode}.png",
            IconPath = $"{basePath}/tiles/icons/{tileCode}.png",
            SmallIconPath = $"{basePath}/tiles/icons/{tileCode}_small.png",
            DiscardSfxPath = $"{ResourcePathConstants.Sfx}/tile_discard.ogg",
            DrawSfxPath = $"{ResourcePathConstants.Sfx}/tile_draw.ogg"
        };
    }

    /// <summary>
    /// 用 Mod 路径约定创建。
    /// </summary>
    public static TileVisual CreateForMod(string tileCode, string modId)
    {
        string basePath = ResourcePathConstants.GetModRoot(modId);
        return new TileVisual
        {
            TileCode = tileCode,
            ModelPath = $"{basePath}/tiles/models/tile_{tileCode}.glb",
            FaceTexturePath = $"{basePath}/tiles/faces/{tileCode}.png",
            IconPath = $"{basePath}/tiles/icons/{tileCode}.png",
            SmallIconPath = $"{basePath}/tiles/icons/{tileCode}_small.png"
        };
    }
}
