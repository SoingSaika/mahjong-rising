using MahjongRising.code.Character;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Player.Actions;
using MahjongRising.code.Resources;
using MahjongRising.code.Yaku;

namespace MahjongRising.code.Mod;

/// <summary>
/// Mod 注册上下文。
/// 
/// Mod 注册新役的方式（只需一步）：
///   context.YakuRules.Register(new MyCustomYakuRule());
///   // MyCustomYakuRule 自带 Definition（含番数、名称、资源路径），一步完成。
///
/// Mod 修改内置役番数的方式：
///   context.YakuRules.Replace(new TanyaoRule() with modified Definition);
///
/// Mod 禁用某个内置役：
///   context.YakuRules.SetEnabled("tanyao", false);
/// </summary>
public class ModRegistrationContext
{
    // ── 规则层 ──
    public ValidatorRegistry Validators { get; init; } = null!;
    public PlayerActionHandlerRegistry ActionHandlers { get; init; } = null!;

    // ── 牌种 ──
    public TileRegistry Tiles { get; init; } = null!;

    // ── 役种（统一注册中心：逻辑 + 数据 + 番数一起注册） ──
    public YakuRuleRegistry YakuRules { get; init; } = null!;

    /// <summary>仍保留 YakuDefinitions 用于注册纯数据定义（图鉴、UI 展示）。</summary>
    public YakuDefinitionRegistry YakuDefinitions { get; init; } = null!;

    // ── 资源/显示 ──
    public ResourceRegistry Resources { get; init; } = null!;

    // ── 角色 ──
    public CharacterRegistry Characters { get; init; } = null!;

    /// <summary>当前 Mod 的 ID。</summary>
    public string CurrentModId { get; set; } = "";

    /// <summary>当前 Mod 的资源根路径。</summary>
    public string ModResourceRoot => ResourcePathConstants.GetModRoot(CurrentModId);
}
