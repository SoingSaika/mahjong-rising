using System.Collections.Generic;

namespace MahjongRising.code.Resources;

/// <summary>
/// 牌面皮肤包。
/// 一套完整的牌面贴图替换方案，可同时覆盖所有牌的正面贴图、图标、牌背、牌模型。
/// 玩家在设置中选择皮肤包后，渲染层从 ResourceRegistry 获取资源时自动叠加皮肤覆盖。
/// Mod 可注册全新皮肤包。
/// </summary>
public class TileSkinPack
{
    /// <summary>皮肤包唯一 ID。</summary>
    public string SkinId { get; init; } = "";

    /// <summary>皮肤包显示名。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>皮肤包描述。</summary>
    public string Description { get; set; } = "";

    /// <summary>皮肤包作者。</summary>
    public string Author { get; set; } = "";

    /// <summary>预览缩略图路径。</summary>
    public string ThumbnailPath { get; set; } = "";

    /// <summary>预览展示图路径（多张牌的效果图）。</summary>
    public string? PreviewImagePath { get; set; }

    // ── 资源根路径 ──

    /// <summary>
    /// 皮肤包资源根路径。
    /// 文件结构与 BuiltinRoot 一致：
    ///   {SkinBasePath}/tiles/faces/{tileCode}.png
    ///   {SkinBasePath}/tiles/icons/{tileCode}.png
    ///   {SkinBasePath}/tiles/backs/back.png
    ///   {SkinBasePath}/tiles/models/tile.glb  （可选）
    /// </summary>
    public string SkinBasePath { get; set; } = "";

    // ── 覆盖范围开关 ──

    /// <summary>是否覆盖牌面贴图。</summary>
    public bool OverrideFaceTextures { get; set; } = true;

    /// <summary>是否覆盖 UI 图标。</summary>
    public bool OverrideIcons { get; set; } = true;

    /// <summary>是否覆盖牌背。</summary>
    public bool OverrideTileBack { get; set; } = true;

    /// <summary>是否覆盖 3D 牌模型。</summary>
    public bool OverrideTileModel { get; set; } = false;

    // ── 逐牌覆盖（仅当需要部分替换时使用） ──

    /// <summary>
    /// 针对特定 TileCode 的覆盖 TileVisual。
    /// 如果为空则按路径约定自动查找。
    /// </summary>
    public Dictionary<string, TileVisual> TileOverrides { get; init; } = new();

    // ── 全局覆盖 ──

    /// <summary>全局牌背贴图覆盖路径。</summary>
    public string? TileBackTexturePath { get; set; }

    /// <summary>全局牌模型覆盖路径。</summary>
    public string? TileModelPath { get; set; }

    // ── Mod 附加 ──

    public Dictionary<string, string> ExtraResources { get; init; } = new();

    /// <summary>
    /// 获取某张牌在此皮肤下的牌面贴图路径。
    /// 如果皮肤包中没有该牌的贴图，返回 null（使用默认）。
    /// </summary>
    public string? GetFaceTexturePath(string tileCode)
    {
        if (TileOverrides.TryGetValue(tileCode, out var vis))
            return vis.FaceTexturePath;

        if (!OverrideFaceTextures)
            return null;

        return $"{SkinBasePath}/tiles/faces/{tileCode}.png";
    }

    /// <summary>获取某张牌在此皮肤下的图标路径。</summary>
    public string? GetIconPath(string tileCode)
    {
        if (TileOverrides.TryGetValue(tileCode, out var vis))
            return vis.IconPath;

        if (!OverrideIcons)
            return null;

        return $"{SkinBasePath}/tiles/icons/{tileCode}.png";
    }
}
