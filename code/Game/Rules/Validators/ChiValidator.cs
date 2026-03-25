using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.Rules.Validators.Builtin;

/// <summary>
/// 标准日麻吃牌验证器。
/// 规则：
///   1. 只能吃上家（左手边）打出的牌
///   2. 只能吃数牌（万/筒/条），不能吃字牌
///   3. 手牌中必须有能组成顺子的搭子
///   4. 立直中不能吃
/// </summary>
public sealed class ChiValidator : ITileActionValidator
{
    public string ValidatorId => "builtin.chi";

    public ValidationResult Validate(
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState targetTile,
        ValidationContext context)
    {
        // 立直中不能吃
        if (player.IsRiichi)
            return ValidationResult.Fail("立直中不能吃牌");

        // 只能吃上家
        int sourceSeat = gameState.ReactionWindow.SourceSeat ?? -1;
        int expectedUpstream = (player.Seat + gameState.PlayerCount - 1) % gameState.PlayerCount;
        if (sourceSeat != expectedUpstream)
            return ValidationResult.Fail("只能吃上家的牌");

        // 字牌不能吃
        var tile = targetTile.Tile;
        if (!IsNumberTile(tile.Category))
            return ValidationResult.Fail("字牌不能吃");

        // 在手牌中寻找所有可能的顺子搭子
        var options = FindChiOptions(player, targetTile);
        if (options.Count == 0)
            return ValidationResult.Fail("手牌中没有可以组成顺子的搭子");

        return ValidationResult.Pass(options);
    }

    private static bool IsNumberTile(string category)
    {
        return category is "man" or "pin" or "sou"
            or "万" or "筒" or "条";
    }

    private static List<ActionOption> FindChiOptions(
        PlayerState player,
        MahjongTileState targetTile)
    {
        var options = new List<ActionOption>();
        var tile = targetTile.Tile;
        int rank = tile.Rank;
        string cat = tile.Category;

        // 手牌中同花色的牌，按 rank 分组
        var sameSuit = player.Hand
            .Where(t => t.Tile.Category == cat && !t.IsLocked)
            .ToList();

        // 三种顺子模式：rank-2,rank-1 / rank-1,rank+1 / rank+1,rank+2
        int[][] patterns = { new[] { -2, -1 }, new[] { -1, 1 }, new[] { 1, 2 } };

        foreach (var pattern in patterns)
        {
            int r1 = rank + pattern[0];
            int r2 = rank + pattern[1];
            if (r1 < 1 || r1 > 9 || r2 < 1 || r2 > 9)
                continue;

            var candidates1 = sameSuit.Where(t => t.Tile.Rank == r1).ToList();
            var candidates2 = sameSuit.Where(t => t.Tile.Rank == r2).ToList();

            if (candidates1.Count == 0 || candidates2.Count == 0)
                continue;

            // 取第一张匹配的（如果有赤宝等变体，可能产生多个选项）
            var t1 = candidates1.First();
            var t2 = candidates2.First();

            var ranks = new[] { r1, r2, rank };
            Array.Sort(ranks);
            string optionId = $"chi_{ranks[0]}_{ranks[1]}_{ranks[2]}";

            options.Add(new ActionOption
            {
                OptionId = optionId,
                InvolvedTileIds = new List<Guid> { t1.InstanceId, t2.InstanceId }
            });

            // 如果有赤宝变体，额外生成选项
            foreach (var alt1 in candidates1.Where(c => c.InstanceId != t1.InstanceId && c.Tile.Variants.Count > 0))
            {
                options.Add(new ActionOption
                {
                    OptionId = $"{optionId}_var_{alt1.InstanceId:N}",
                    InvolvedTileIds = new List<Guid> { alt1.InstanceId, t2.InstanceId }
                });
            }

            foreach (var alt2 in candidates2.Where(c => c.InstanceId != t2.InstanceId && c.Tile.Variants.Count > 0))
            {
                options.Add(new ActionOption
                {
                    OptionId = $"{optionId}_var_{alt2.InstanceId:N}",
                    InvolvedTileIds = new List<Guid> { t1.InstanceId, alt2.InstanceId }
                });
            }
        }

        return options;
    }
}
