namespace MahjongRising.code.Resources;

/// <summary>
/// 资源路径约定常量。
/// 所有内置资源路径前缀统一定义在此处。
/// Mod 资源使用 user://mods/{modId}/ 前缀。
///
/// Godot 路径规范：
///   res:// = 项目内置资源（只读）
///   user:// = 用户数据目录（可写，Mod 放这里）
/// </summary>
public static class ResourcePathConstants
{
    // ── 根前缀 ──
    public const string BuiltinRoot = "res://assets";
    public const string ModRoot = "user://mods";

    // ── 牌面资源 ──
    public const string TileModels = BuiltinRoot + "/tiles/models";        // 3D 牌模型 (.glb/.tscn)
    public const string TileFaceTextures = BuiltinRoot + "/tiles/faces";   // 牌面贴图 (.png)
    public const string TileIcons = BuiltinRoot + "/tiles/icons";          // UI 小图标 (.png)
    public const string TileBackTextures = BuiltinRoot + "/tiles/backs";   // 牌背贴图 (.png)

    // ── 桌面/场景资源 ──
    public const string TableModels = BuiltinRoot + "/table/models";       // 3D 麻将桌模型
    public const string TableTextures = BuiltinRoot + "/table/textures";   // 桌布/毡布贴图
    public const string TableEnvironments = BuiltinRoot + "/table/env";    // 环境光/HDRI

    // ── UI 资源 ──
    public const string ActionButtons = BuiltinRoot + "/ui/actions";       // 动作按钮贴图
    public const string UiIcons = BuiltinRoot + "/ui/icons";               // 通用 UI 图标
    public const string UiPanels = BuiltinRoot + "/ui/panels";             // 面板/背景
    public const string UiFonts = BuiltinRoot + "/ui/fonts";               // 字体

    // ── 角色资源 ──
    public const string CharacterModels = BuiltinRoot + "/characters/models";       // 3D 角色模型
    public const string CharacterPortraits = BuiltinRoot + "/characters/portraits"; // 立绘
    public const string CharacterIcons = BuiltinRoot + "/characters/icons";         // 头像图标
    public const string CharacterVoice = BuiltinRoot + "/characters/voice";         // 语音

    // ── 特效/动画 ──
    public const string VfxParticles = BuiltinRoot + "/vfx/particles";     // 粒子特效
    public const string VfxShaders = BuiltinRoot + "/vfx/shaders";         // 着色器
    public const string Animations = BuiltinRoot + "/animations";          // 通用动画

    // ── 音效/音乐 ──
    public const string Sfx = BuiltinRoot + "/audio/sfx";                  // 音效
    public const string Music = BuiltinRoot + "/audio/music";              // BGM

    // ── 役种显示 ──
    public const string YakuBanners = BuiltinRoot + "/ui/yaku";            // 役名弹出横幅

    /// <summary>获取 Mod 的资源根路径。</summary>
    public static string GetModRoot(string modId) => $"{ModRoot}/{modId}";

    /// <summary>获取皮肤包的资源根路径。</summary>
    public static string GetSkinRoot(string skinId) => $"{BuiltinRoot}/skins/{skinId}";
}
