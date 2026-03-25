namespace MahjongRising.code.Mod;

/// <summary>
/// Mod 入口接口。
/// 所有 Mod 必须实现此接口，引擎启动时自动扫描并调用 Register。
///
/// Mod 可以做的事（全部通过 context 注入，不修改源码）：
///   - context.YakuRules.Register(rule)          添加新役（逻辑+番数一步完成）
///   - context.YakuRules.Replace(rule)           替换内置役（改番数或判定逻辑）
///   - context.YakuRules.SetEnabled(id, false)   禁用内置役
///   - context.Validators.Register(type, v)      添加新动作验证器
///   - context.Tiles.Register(code, factory, n)  添加新牌种
///   - context.Resources.RegisterTileVisual(v)   添加牌面外观
///   - context.Resources.RegisterTableTheme(t)   添加桌面主题
///   - context.Resources.RegisterSkinPack(s)     添加皮肤包
///   - context.Characters.RegisterCharacter(c)   添加角色
///   - context.Characters.RegisterAbility(a)     添加角色能力
///   - context.ActionHandlers.Register(handler)  添加玩家动作处理器
///   - context.Resources.RegisterActionButton(b) 添加动作按钮外观
/// </summary>
public interface IMahjongMod
{
    string ModId { get; }
    string DisplayName { get; }
    string Version { get; }

    void Register(ModRegistrationContext context);
    void Unregister(ModRegistrationContext context);
}