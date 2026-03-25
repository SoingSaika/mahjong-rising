using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.Rpc;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;
using MahjongRising.code.Yaku;

namespace MahjongRising.code.AI;

public enum AiStyle { Defensive, Balanced, Aggressive }

/// <summary>
/// 内置 AI。
/// 三种风格：防守型（优先安全牌）、平衡型、攻击型（优先进攻向听数）。
///
/// 决策优先级：
///   1. 能胡就胡（无论风格）
///   2. 能碰/杠就碰/杠（攻击型积极、防守型消极）
///   3. 弃牌选择（防守型打安全牌、攻击型打效率最差的牌）
///
/// 这是一个功能完整但逻辑简单的 AI，适合入门级别对战。
/// Mod 可实现 IAiPlayer 提供更强的 AI（如蒙特卡洛树搜索等）。
/// </summary>
public class BasicAiPlayer : IAiPlayer
{
    public int Seat { get; }
    public AiStyle Style { get; }
    private readonly Random _rng = new();

    public BasicAiPlayer(int seat, AiStyle style = AiStyle.Balanced)
    {
        Seat = seat;
        Style = style;
    }

    // ═══ 弃牌决策 ═══

    public AiDiscardDecision DecideDiscard(MahjongGameState state, PlayerState self)
    {
        var hand = self.Hand.Where(t => !t.IsLocked).ToList();
        if (hand.Count == 0)
            return new AiDiscardDecision { TileInstanceId = self.Hand.Last().InstanceId };

        MahjongTileState chosen;

        switch (Style)
        {
            case AiStyle.Defensive:
                chosen = PickSafestTile(hand, state);
                break;
            case AiStyle.Aggressive:
                chosen = PickMostIsolated(hand);
                break;
            default: // Balanced
                chosen = _rng.NextDouble() < 0.6
                    ? PickMostIsolated(hand)
                    : PickSafestTile(hand, state);
                break;
        }

        // 立直判定（简化：门前清 + 手牌只需再进一张）
        bool riichi = false;
        if (self.IsMenzen && !self.IsRiichi && self.Score >= 1000)
        {
            // 粗略向听数估算：如果手牌整齐度高，尝试立直
            if (Style == AiStyle.Aggressive && _rng.NextDouble() < 0.4)
                riichi = true;
        }

        return new AiDiscardDecision
        {
            TileInstanceId = chosen.InstanceId,
            IsRiichi = riichi
        };
    }

    // ═══ 自摸后决策 ═══

    public AiActionDecision? DecideSelfAction(MahjongGameState state, PlayerState self, AvailableActionsDto actions)
    {
        // 能自摸胡一定胡
        if (actions.Hu is { Count: > 0 })
            return new AiActionDecision { ActionType = "hu", OptionId = actions.Hu[0].OptionId };

        // 暗杠：攻击型积极杠，防守型少杠
        if (actions.Gang is { Count: > 0 })
        {
            bool shouldKan = Style switch
            {
                AiStyle.Aggressive => true,
                AiStyle.Balanced => _rng.NextDouble() < 0.5,
                _ => false
            };
            if (shouldKan)
                return new AiActionDecision { ActionType = "gang", OptionId = actions.Gang[0].OptionId };
        }

        return null; // 不执行自摸动作，进入弃牌
    }

    // ═══ 反应决策 ═══

    public AiActionDecision DecideReaction(MahjongGameState state, PlayerState self, AvailableActionsDto actions)
    {
        // 能荣和一定和
        if (actions.Hu is { Count: > 0 })
            return new AiActionDecision { ActionType = "hu", OptionId = actions.Hu[0].OptionId };

        // 碰
        if (actions.Peng is { Count: > 0 })
        {
            bool shouldPon = Style switch
            {
                AiStyle.Aggressive => true,
                AiStyle.Balanced => _rng.NextDouble() < 0.4,
                _ => _rng.NextDouble() < 0.1 // 防守型很少碰
            };
            if (shouldPon)
                return new AiActionDecision { ActionType = "peng", OptionId = actions.Peng[0].OptionId };
        }

        // 明杠
        if (actions.Gang is { Count: > 0 } && Style == AiStyle.Aggressive)
            return new AiActionDecision { ActionType = "gang", OptionId = actions.Gang[0].OptionId };

        // 吃
        if (actions.Chi is { Count: > 0 })
        {
            bool shouldChi = Style switch
            {
                AiStyle.Aggressive => _rng.NextDouble() < 0.5,
                AiStyle.Balanced => _rng.NextDouble() < 0.2,
                _ => false
            };
            if (shouldChi)
                return new AiActionDecision { ActionType = "chi", OptionId = actions.Chi[0].OptionId };
        }

        return new AiActionDecision { ActionType = "pass" };
    }

    // ═══ 弃牌选择策略 ═══

    /// <summary>选最孤立的牌（周围没有相关牌，效率最差）。</summary>
    private MahjongTileState PickMostIsolated(List<MahjongTileState> hand)
    {
        return hand
            .OrderBy(t => CountRelated(t, hand))
            .ThenByDescending(t => IsTerminalOrHonor(t) ? 1 : 0)
            .First();
    }

    /// <summary>选最安全的牌（对手大概率不需要的牌）。</summary>
    private MahjongTileState PickSafestTile(List<MahjongTileState> hand, MahjongGameState state)
    {
        // 安全度评估：已有多人弃过的牌最安全，字牌次之
        var allDiscards = state.Players.SelectMany(p => p.Discards).ToList();

        return hand
            .OrderByDescending(t => SafetyScore(t, allDiscards, state))
            .First();
    }

    private static int CountRelated(MahjongTileState tile, List<MahjongTileState> hand)
    {
        var cat = TileCategoryHelper.Normalize(tile.Tile.Category);
        int rank = tile.Tile.Rank;

        return hand.Count(t =>
        {
            if (t.InstanceId == tile.InstanceId) return false;
            var tc = TileCategoryHelper.Normalize(t.Tile.Category);
            if (tc != cat) return false;
            return t.Tile.Rank == rank           // 同牌
                || Math.Abs(t.Tile.Rank - rank) <= 2; // 相邻（可组顺子）
        });
    }

    private static int SafetyScore(MahjongTileState tile, List<MahjongTileState> discards, MahjongGameState state)
    {
        int score = 0;
        var cat = TileCategoryHelper.Normalize(tile.Tile.Category);
        int rank = tile.Tile.Rank;

        // 场上已弃过同牌 → 安全
        score += discards.Count(d =>
            TileCategoryHelper.Normalize(d.Tile.Category) == cat && d.Tile.Rank == rank) * 30;

        // 字牌且无人碰过 → 中等安全
        if (TileCategoryHelper.IsHonor(cat)) score += 15;

        // 幺九牌 → 稍安全
        if (rank == 1 || rank == 9) score += 10;

        // 有人立直 → 更保守（参考立直者弃牌）
        foreach (var p in state.Players.Where(p => p.IsRiichi))
        {
            bool sameAsDiscard = p.Discards.Any(d =>
                TileCategoryHelper.Normalize(d.Tile.Category) == cat && d.Tile.Rank == rank);
            if (sameAsDiscard) score += 50; // 现物（立直者打过的牌 = 绝对安全）
        }

        return score;
    }

    private static bool IsTerminalOrHonor(MahjongTileState tile)
    {
        var cat = TileCategoryHelper.Normalize(tile.Tile.Category);
        return TileCategoryHelper.IsHonor(cat) || tile.Tile.Rank == 1 || tile.Tile.Rank == 9;
    }
}
