namespace MahjongRising.code.Resources;

/// <summary>
/// 当前局面的完整视觉配置。
/// 由房主或服务器在开局时确定，同步给所有客户端。
/// </summary>
public class GameVisualConfig
{
    /// <summary>当前使用的桌面主题 ID。</summary>
    public string TableThemeId { get; set; } = "builtin.classic";

    /// <summary>当前使用的牌面皮肤包 ID（空 = 默认）。</summary>
    public string? TileSkinId { get; set; }

    /// <summary>3D 摄像机预设（"overhead" / "first_person" / "spectator"）。</summary>
    public string CameraPreset { get; set; } = "overhead";

    /// <summary>是否启用牌面反射效果。</summary>
    public bool EnableTileReflection { get; set; } = true;

    /// <summary>是否启用桌面阴影。</summary>
    public bool EnableTableShadows { get; set; } = true;

    /// <summary>是否启用动作宣告横幅动画。</summary>
    public bool EnableActionBanners { get; set; } = true;

    /// <summary>UI 缩放倍率（1.0 = 默认）。</summary>
    public float UiScale { get; set; } = 1.0f;

    /// <summary>语言（影响役名显示等）。"ja" / "zh" / "en"。</summary>
    public string Language { get; set; } = "ja";
}
