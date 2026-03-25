using System.Collections.Generic;
using MahjongRising.code.Resources;

namespace MahjongRising.code.Character;

/// <summary>
/// 角色定义。
/// 包含一个可选角色的所有数据：立绘、3D 模型、语音包、被动能力、显示信息。
/// Mod 可通过 CharacterRegistry 注册全新角色。
///
/// 角色能力与牌技能的区别：
///   - MahjongAction（牌技能）：绑定在牌上，任何人摸到都会触发。
///   - CharacterAbility（角色能力）：绑定在角色上，只有选该角色的玩家才有。
/// </summary>
public class CharacterDefinition
{
    /// <summary>角色唯一 ID。</summary>
    public string CharacterId { get; init; } = "";

    // ── 基本信息 ──

    /// <summary>角色名（本地化键或直接文本）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>角色名（日文）。</summary>
    public string DisplayNameJp { get; set; } = "";

    /// <summary>角色名（中文）。</summary>
    public string DisplayNameCn { get; set; } = "";

    /// <summary>角色称号/头衔。</summary>
    public string Title { get; set; } = "";

    /// <summary>角色描述/背景故事。</summary>
    public string Description { get; set; } = "";

    /// <summary>角色稀有度（"common" / "rare" / "epic" / "legendary"），影响解锁条件。</summary>
    public string Rarity { get; set; } = "common";

    // ── 3D 模型资源 ──

    /// <summary>3D 角色模型路径（.glb/.tscn），在麻将桌旁展示。</summary>
    public string ModelPath { get; set; } = "";

    /// <summary>模型动画控制器路径（.tres/.tscn），包含待机、摸牌、弃牌、胡牌等动画。</summary>
    public string? AnimationTreePath { get; set; }

    /// <summary>角色模型材质覆盖路径（可选，用于换色/换装）。</summary>
    public string? MaterialOverridePath { get; set; }

    // ── 2D 立绘资源 ──

    /// <summary>全身立绘路径（高分辨率，用于角色选择界面）。</summary>
    public string PortraitFullPath { get; set; } = "";

    /// <summary>半身立绘路径（用于对局中的角色展示框）。</summary>
    public string PortraitHalfPath { get; set; } = "";

    /// <summary>头像图标路径（64x64 或 128x128，用于 HUD / 计分板）。</summary>
    public string AvatarIconPath { get; set; } = "";

    /// <summary>表情立绘集（键 = 表情 ID："neutral" / "happy" / "angry" / "shock" / "win" / "lose"）。</summary>
    public Dictionary<string, string> ExpressionPortraits { get; init; } = new();

    // ── 语音资源 ──

    /// <summary>角色语音包。</summary>
    public CharacterVoicePack VoicePack { get; set; } = new();

    // ── 角色能力 ──

    /// <summary>
    /// 角色被动能力列表。
    /// 由 CharacterAbility 子类实现，在 GameBootstrap 中注册到引擎。
    /// </summary>
    public List<string> AbilityIds { get; init; } = new();

    /// <summary>角色能力描述文本（用于 UI 展示）。</summary>
    public List<CharacterAbilityDescription> AbilityDescriptions { get; init; } = new();

    // ── 对局中动画触发配置 ──

    /// <summary>
    /// 3D 模型动画名称映射。
    /// 键 = 游戏事件（"draw" / "discard" / "chi" / "peng" / "gang" / "hu" / "riichi" / "idle" / "win" / "lose"），
    /// 值 = 模型中的动画名称。
    /// </summary>
    public Dictionary<string, string> AnimationMap { get; init; } = new()
    {
        ["idle"] = "idle",
        ["draw"] = "draw_tile",
        ["discard"] = "discard_tile",
        ["chi"] = "call_chi",
        ["peng"] = "call_peng",
        ["gang"] = "call_gang",
        ["hu"] = "call_hu",
        ["riichi"] = "declare_riichi",
        ["win"] = "victory",
        ["lose"] = "defeat",
        ["think"] = "thinking"
    };

    // ── Mod 附加数据 ──

    public Dictionary<string, string> ExtraResources { get; init; } = new();
    public Dictionary<string, object> ExtraData { get; init; } = new();

    /// <summary>创建内置角色时的工厂方法。</summary>
    public static CharacterDefinition CreateBuiltin(string charId, string name, string nameJp, string nameCn)
    {
        string b = ResourcePathConstants.BuiltinRoot;
        return new CharacterDefinition
        {
            CharacterId = charId,
            DisplayName = name,
            DisplayNameJp = nameJp,
            DisplayNameCn = nameCn,
            ModelPath = $"{b}/characters/models/{charId}.glb",
            AnimationTreePath = $"{b}/characters/models/{charId}_anim.tres",
            PortraitFullPath = $"{b}/characters/portraits/{charId}_full.png",
            PortraitHalfPath = $"{b}/characters/portraits/{charId}_half.png",
            AvatarIconPath = $"{b}/characters/icons/{charId}.png",
            ExpressionPortraits = new()
            {
                ["neutral"] = $"{b}/characters/portraits/{charId}_neutral.png",
                ["happy"] = $"{b}/characters/portraits/{charId}_happy.png",
                ["angry"] = $"{b}/characters/portraits/{charId}_angry.png",
                ["shock"] = $"{b}/characters/portraits/{charId}_shock.png",
                ["win"] = $"{b}/characters/portraits/{charId}_win.png",
                ["lose"] = $"{b}/characters/portraits/{charId}_lose.png"
            }
        };
    }
}

/// <summary>
/// 角色能力的 UI 描述信息（纯数据，不含逻辑）。
/// </summary>
public class CharacterAbilityDescription
{
    /// <summary>能力 ID（与 ICharacterAbility.AbilityId 对应）。</summary>
    public string AbilityId { get; init; } = "";

    /// <summary>能力名。</summary>
    public string Name { get; set; } = "";

    /// <summary>能力名（日文）。</summary>
    public string NameJp { get; set; } = "";

    /// <summary>能力名（中文）。</summary>
    public string NameCn { get; set; } = "";

    /// <summary>能力效果描述。</summary>
    public string Description { get; set; } = "";

    /// <summary>能力图标路径。</summary>
    public string IconPath { get; set; } = "";

    /// <summary>能力类型标签（"passive" / "active" / "trigger"）。</summary>
    public string AbilityType { get; set; } = "passive";
}
