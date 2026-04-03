using System.Collections.Generic;
using System.Linq;

namespace MahjongRising.code.Resources;

/// <summary>
/// 资源总注册中心。
/// 管理牌面视觉、动作按钮、桌面主题、皮肤包的注册与查询。
/// 查询时自动叠加皮肤覆盖层：皮肤 > 主题覆盖 > 默认资源。
/// Mod 通过 ModRegistrationContext 访问此注册中心注入资源。
/// </summary>
public sealed class ResourceRegistry
{
    // ── 牌面视觉 ──
    private readonly Dictionary<string, TileVisual> _tileVisuals = new();

    // ── 动作按钮 ──
    private readonly Dictionary<string, ActionButtonVisual> _actionButtons = new();

    // ── 桌面主题 ──
    private readonly Dictionary<string, TableTheme> _tableThemes = new();

    // ── 皮肤包 ──
    private readonly Dictionary<string, TileSkinPack> _skinPacks = new();

    // ── 当前选择 ──
    private string? _activeSkinId;
    private string _activeThemeId = "builtin.classic";

    // ═══════════════════════════════
    // 牌面视觉
    // ═══════════════════════════════

    public void RegisterTileVisual(TileVisual visual)
    {
        _tileVisuals[visual.TileCode] = visual;
    }

    public void UnregisterTileVisual(string tileCode)
    {
        _tileVisuals.Remove(tileCode);
    }

    /// <summary>
    /// 获取某张牌的最终视觉资源。
    /// 优先级：激活的皮肤包覆盖 > 注册的 TileVisual > null。
    /// </summary>
    public TileVisual? GetTileVisual(string tileCode)
    {
        _tileVisuals.TryGetValue(tileCode, out var baseVisual);
        if (baseVisual == null) return null;

        // 应用皮肤覆盖
        if (_activeSkinId != null && _skinPacks.TryGetValue(_activeSkinId, out var skin))
        {
            return ApplySkinOverride(baseVisual, skin);
        }

        return baseVisual;
    }

    public IReadOnlyList<TileVisual> GetAllTileVisuals()
        => _tileVisuals.Values.ToList();

    // ═══════════════════════════════
    // 动作按钮
    // ═══════════════════════════════

    public void RegisterActionButton(ActionButtonVisual button)
    {
        _actionButtons[button.ActionType] = button;
    }

    public void UnregisterActionButton(string actionType)
    {
        _actionButtons.Remove(actionType);
    }

    public ActionButtonVisual? GetActionButton(string actionType)
    {
        _actionButtons.TryGetValue(actionType, out var btn);
        return btn;
    }

    /// <summary>获取所有已注册的动作按钮，按 DisplayOrder 降序排列。</summary>
    public IReadOnlyList<ActionButtonVisual> GetAllActionButtons()
        => _actionButtons.Values.OrderByDescending(b => b.DisplayOrder).ToList();

    // ═══════════════════════════════
    // 桌面主题
    // ═══════════════════════════════

    public void RegisterTableTheme(TableTheme theme)
    {
        _tableThemes[theme.ThemeId] = theme;
    }

    public void UnregisterTableTheme(string themeId)
    {
        _tableThemes.Remove(themeId);
    }

    public TableTheme? GetTableTheme(string themeId)
    {
        _tableThemes.TryGetValue(themeId, out var theme);
        return theme;
    }

    public TableTheme? GetActiveTableTheme()
        => GetTableTheme(_activeThemeId);

    public IReadOnlyList<TableTheme> GetAllTableThemes()
        => _tableThemes.Values.ToList();

    public void SetActiveTheme(string themeId) => _activeThemeId = themeId;

    // ═══════════════════════════════
    // 皮肤包
    // ═══════════════════════════════

    public void RegisterSkinPack(TileSkinPack skin)
    {
        _skinPacks[skin.SkinId] = skin;
    }

    public void UnregisterSkinPack(string skinId)
    {
        _skinPacks.Remove(skinId);
        if (_activeSkinId == skinId) _activeSkinId = null;
    }

    public TileSkinPack? GetSkinPack(string skinId)
    {
        _skinPacks.TryGetValue(skinId, out var skin);
        return skin;
    }

    public IReadOnlyList<TileSkinPack> GetAllSkinPacks()
        => _skinPacks.Values.ToList();

    public void SetActiveSkin(string? skinId) => _activeSkinId = skinId;
    public string? GetActiveSkinId() => _activeSkinId;

    // ═══════════════════════════════
    // 皮肤覆盖逻辑
    // ═══════════════════════════════

    private static TileVisual ApplySkinOverride(TileVisual baseVisual, TileSkinPack skin)
    {
        // 如果皮肤有逐牌覆盖，直接返回
        if (skin.TileOverrides.TryGetValue(baseVisual.TileCode, out var fullOverride))
            return fullOverride;

        // 按约定路径覆盖
        var result = new TileVisual
        {
            TileCode = baseVisual.TileCode,
            ModelPath = skin.OverrideTileModel && skin.TileModelPath != null
                ? skin.TileModelPath : baseVisual.ModelPath,
            FaceTexturePath = skin.GetFaceTexturePath(baseVisual.TileCode) ?? baseVisual.FaceTexturePath,
            FaceNormalMapPath = baseVisual.FaceNormalMapPath,
            BackTexturePath = skin.OverrideTileBack ? skin.TileBackTexturePath : baseVisual.BackTexturePath,
            IconPath = skin.GetIconPath(baseVisual.TileCode) ?? baseVisual.IconPath,
            SmallIconPath = baseVisual.SmallIconPath,
            DisabledIconPath = baseVisual.DisabledIconPath,
            DrawVfxPath = baseVisual.DrawVfxPath,
            DiscardVfxPath = baseVisual.DiscardVfxPath,
            HighlightShaderPath = baseVisual.HighlightShaderPath,
            DiscardSfxPath = baseVisual.DiscardSfxPath,
            DrawSfxPath = baseVisual.DrawSfxPath
        };
        foreach (var kv in baseVisual.ExtraResources)
            result.ExtraResources[kv.Key] = kv.Value;

        return result;
    }
}
