using System;
using System.Collections.Generic;

namespace MahjongRising.code.Game.State;

/// <summary>
/// 一条局内事件记录，用于回放、Mod 钩子、以及增量同步。
/// Mod 可以自定义 EventType 字符串。
/// </summary>
public class GameEvent
{
    public string EventType { get; init; } = "";
    public int Seat { get; init; } = -1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Payload { get; init; } = new();

    // ── 内置事件类型常量 ──
    public const string Draw = "draw";
    public const string Discard = "discard";
    public const string Chi = "chi";
    public const string Peng = "peng";
    public const string Gang = "gang";
    public const string Hu = "hu";
    public const string Riichi = "riichi";
    public const string RoundStart = "round_start";
    public const string RoundEnd = "round_end";
    public const string TurnStart = "turn_start";
    public const string ReactionOpen = "reaction_open";
    public const string ReactionResolve = "reaction_resolve";
}
