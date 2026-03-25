using System.Threading.Tasks;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Character;

/// <summary>
/// 角色能力接口。
/// 与 MahjongAction（牌技能）不同，角色能力只对选择了该角色的玩家生效。
/// 在肉鸽模式中，角色能力可以通过升级/解锁获得。
/// Mod 可实现此接口添加自定义角色能力。
///
/// 生命周期钩子：
///   - OnRoundStart:  每局开始时
///   - OnTurnStart:   每回合开始时
///   - OnDraw:        摸牌后
///   - OnBeforeDiscard: 弃牌前（可修改/取消弃牌）
///   - OnAfterDiscard:  弃牌后
///   - OnMeld:        副露后（吃/碰/杠）
///   - OnWin:         和牌时
///   - OnLose:        放铳 / 局结束未和时
///   - OnScoreChange: 分数变动时
/// </summary>
public interface ICharacterAbility
{
    /// <summary>能力唯一 ID。</summary>
    string AbilityId { get; }

    /// <summary>每局开始时调用。</summary>
    Task OnRoundStart(CharacterAbilityContext ctx) => Task.CompletedTask;

    /// <summary>回合开始时调用。</summary>
    Task OnTurnStart(CharacterAbilityContext ctx) => Task.CompletedTask;

    /// <summary>摸牌后调用。</summary>
    Task OnDraw(CharacterAbilityContext ctx, MahjongTileState drawnTile) => Task.CompletedTask;

    /// <summary>弃牌前调用。返回 false 可阻止弃牌。</summary>
    Task<bool> OnBeforeDiscard(CharacterAbilityContext ctx, MahjongTileState tile)
        => Task.FromResult(true);

    /// <summary>弃牌后调用。</summary>
    Task OnAfterDiscard(CharacterAbilityContext ctx, MahjongTileState tile) => Task.CompletedTask;

    /// <summary>副露后调用。</summary>
    Task OnMeld(CharacterAbilityContext ctx, MeldGroup meld) => Task.CompletedTask;

    /// <summary>和牌时调用。</summary>
    Task OnWin(CharacterAbilityContext ctx) => Task.CompletedTask;

    /// <summary>失败时调用（放铳 / 流局失利）。</summary>
    Task OnLose(CharacterAbilityContext ctx) => Task.CompletedTask;

    /// <summary>分数变化时调用。</summary>
    Task OnScoreChange(CharacterAbilityContext ctx, int oldScore, int newScore) => Task.CompletedTask;
}

/// <summary>
/// 角色能力执行上下文。
/// </summary>
public class CharacterAbilityContext
{
    public MahjongGameState GameState { get; init; } = null!;
    public PlayerState Player { get; init; } = null!;
    public CharacterDefinition Character { get; init; } = null!;

    /// <summary>能力等级（肉鸽模式中可升级）。1 = 基础。</summary>
    public int AbilityLevel { get; init; } = 1;

    /// <summary>是否服务器权威执行。</summary>
    public bool IsServerAuthoritative { get; init; } = true;
}
