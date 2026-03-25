using System.Collections.Generic;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.Rules.Validators;

/// <summary>
/// 牌动作验证器接口。
/// Mod 实现此接口来定义新的动作验证规则（吃/碰/杠/胡/自定义）。
/// 通过 ValidatorRegistry 注册后，由 MahjongRuleEngine 自动调用。
/// </summary>
public interface ITileActionValidator
{
    /// <summary>验证器唯一 ID（Mod 需保证全局唯一）。</summary>
    string ValidatorId { get; }

    /// <summary>
    /// 验证某位玩家能否对目标牌执行某种动作。
    /// </summary>
    /// <param name="gameState">当前局面</param>
    /// <param name="player">执行动作的玩家</param>
    /// <param name="targetTile">目标牌（被吃/碰的牌，或自摸的牌）</param>
    /// <param name="context">额外上下文（如吃牌时手牌中选择的搭子）</param>
    /// <returns>验证结果</returns>
    ValidationResult Validate(
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState targetTile,
        ValidationContext context);
}

/// <summary>验证结果。</summary>
public class ValidationResult
{
    public bool IsValid { get; init; }
    public string Reason { get; init; } = "";

    /// <summary>如果 IsValid=true，包含可执行的具体选项（如吃的多种搭子）。</summary>
    public List<ActionOption> Options { get; init; } = new();

    public static ValidationResult Pass(List<ActionOption>? options = null)
        => new() { IsValid = true, Options = options ?? new() };

    public static ValidationResult Fail(string reason)
        => new() { IsValid = false, Reason = reason };
}

/// <summary>一个可执行的动作选项（如吃的某种搭子组合）。</summary>
public class ActionOption
{
    /// <summary>选项标识（如 "chi_12" 表示用 1-2 吃 3）。</summary>
    public string OptionId { get; init; } = "";

    /// <summary>参与的手牌 InstanceId 列表。</summary>
    public List<System.Guid> InvolvedTileIds { get; init; } = new();

    /// <summary>Mod 自定义附加数据。</summary>
    public Dictionary<string, object> Extra { get; init; } = new();
}

/// <summary>验证上下文：传递额外参数给验证器。</summary>
public class ValidationContext
{
    /// <summary>动作类型标识。</summary>
    public string ActionType { get; init; } = "";

    /// <summary>指定的手牌（如吃时选择的搭子）。</summary>
    public List<System.Guid> SelectedTileIds { get; init; } = new();

    /// <summary>Mod 用额外参数。</summary>
    public Dictionary<string, object> Extra { get; init; } = new();
}
