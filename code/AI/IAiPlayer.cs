using System;
using MahjongRising.code.Game.State;
using MahjongRising.code.Player.States;
using MahjongRising.code.Session.Rpc;

namespace MahjongRising.code.AI;

/// <summary>
/// AI 玩家接口。
/// 每个方法对应一个决策点，返回 AI 选择的动作。
/// Mod 可实现此接口提供自定义 AI。
/// </summary>
public interface IAiPlayer
{
    /// <summary>AI 座位。</summary>
    int Seat { get; }

    /// <summary>
    /// 弃牌决策：选择手牌中一张打出。
    /// 返回要打出的牌的 InstanceId，以及是否宣告立直。
    /// </summary>
    AiDiscardDecision DecideDiscard(MahjongGameState state, PlayerState self);

    /// <summary>
    /// 自摸后决策：是否执行暗杠/加杠/自摸胡，还是直接弃牌。
    /// 返回 null = 不执行自摸动作，进入弃牌。
    /// </summary>
    AiActionDecision? DecideSelfAction(MahjongGameState state, PlayerState self, AvailableActionsDto actions);

    /// <summary>
    /// 反应决策：对他人弃牌是否吃/碰/杠/胡/跳过。
    /// 返回 "pass" = 跳过。
    /// </summary>
    AiActionDecision DecideReaction(MahjongGameState state, PlayerState self, AvailableActionsDto actions);
}

/// <summary>AI 弃牌决策。</summary>
public class AiDiscardDecision
{
    public Guid TileInstanceId { get; init; }
    public bool IsRiichi { get; init; }
}

/// <summary>AI 动作决策（吃/碰/杠/胡/自摸 etc）。</summary>
public class AiActionDecision
{
    public string ActionType { get; init; } = "pass";
    public string OptionId { get; init; } = "";
}
