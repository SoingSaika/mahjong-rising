using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.State;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;
using MahjongRising.code.Yaku;

namespace MahjongRising.code.Game.Rules.Validators.Builtin;

/// <summary>
/// 和牌验证器（新版）。
/// 使用 YakuRuleRegistry 代替旧的 IYakuEvaluator 列表。
/// 手牌只解析一次，通过 YakuEvalContext 共享给所有 IYakuRule。
/// </summary>
public sealed class HuValidator : ITileActionValidator
{
    public string ValidatorId => "builtin.hu";

    private readonly YakuRuleRegistry _yakuRules;

    public HuValidator(YakuRuleRegistry yakuRules)
    {
        _yakuRules = yakuRules;
    }

    public ValidationResult Validate(
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState targetTile,
        ValidationContext context)
    {
        // 把目标牌加入临时手牌来判定
        var tempHand = player.Hand.Select(t => t.Tile).ToList();
        tempHand.Add(targetTile.Tile);

        // 基本和牌形检查
        bool isWinningHand = CheckStandardForm(tempHand)
            || CheckSevenPairs(tempHand)
            || CheckThirteenOrphans(tempHand);

        if (!isWinningHand)
            return ValidationResult.Fail("不是有效的和牌形");

        // 创建共享上下文（手牌只解析这一次）
        var evalCtx = YakuEvalContext.Create(gameState, player, targetTile, tempHand);

        // 执行所有役种规则判定
        var yakuList = _yakuRules.EvaluateAll(evalCtx);

        if (yakuList.Count == 0)
            return ValidationResult.Fail("无役，不能和牌");

        int totalHan = yakuList.Sum(y => y.Han);
        bool isTsumo = gameState.CurrentTurnSeat == player.Seat;

        return ValidationResult.Pass(new List<ActionOption>
        {
            new()
            {
                OptionId = isTsumo ? "tsumo" : "ron",
                Extra = new Dictionary<string, object>
                {
                    ["yaku"] = yakuList.Select(y => y.YakuId).ToList(),
                    ["han"] = totalHan,
                    ["is_tsumo"] = isTsumo
                }
            }
        });
    }

    // ── 和牌形检查（与之前相同） ──

    private static bool CheckStandardForm(List<Mahjong.Tiles.MahjongTile> tiles)
    {
        if (tiles.Count < 14) return false;
        var counts = new Dictionary<(string, int), int>();
        foreach (var t in tiles)
        {
            var key = (TileCategoryHelper.Normalize(t.Category), t.Rank);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        foreach (var pair in counts.Where(kv => kv.Value >= 2))
        {
            var remaining = new Dictionary<(string, int), int>(counts);
            remaining[pair.Key] -= 2;
            if (remaining[pair.Key] == 0) remaining.Remove(pair.Key);
            if (CanFormMentsu(remaining)) return true;
        }
        return false;
    }

    private static bool CanFormMentsu(Dictionary<(string, int), int> counts)
    {
        if (counts.Count == 0) return true;
        var first = counts.OrderBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item2).First();
        var (cat, rank) = first.Key;

        if (first.Value >= 3)
        {
            var copy = new Dictionary<(string, int), int>(counts);
            copy[(cat, rank)] -= 3;
            if (copy[(cat, rank)] == 0) copy.Remove((cat, rank));
            if (CanFormMentsu(copy)) return true;
        }

        if (TileCategoryHelper.IsNumber(cat) && rank <= 7
            && counts.GetValueOrDefault((cat, rank + 1)) >= 1
            && counts.GetValueOrDefault((cat, rank + 2)) >= 1)
        {
            var copy = new Dictionary<(string, int), int>(counts);
            copy[(cat, rank)] -= 1; copy[(cat, rank + 1)] -= 1; copy[(cat, rank + 2)] -= 1;
            if (copy[(cat, rank)] == 0) copy.Remove((cat, rank));
            if (copy[(cat, rank + 1)] == 0) copy.Remove((cat, rank + 1));
            if (copy[(cat, rank + 2)] == 0) copy.Remove((cat, rank + 2));
            if (CanFormMentsu(copy)) return true;
        }
        return false;
    }

    private static bool CheckSevenPairs(List<Mahjong.Tiles.MahjongTile> tiles)
    {
        if (tiles.Count != 14) return false;
        var groups = tiles.GroupBy(t => (TileCategoryHelper.Normalize(t.Category), t.Rank)).ToList();
        return groups.Count == 7 && groups.All(g => g.Count() == 2);
    }

    private static bool CheckThirteenOrphans(List<Mahjong.Tiles.MahjongTile> tiles)
    {
        if (tiles.Count != 14) return false;
        var required = new HashSet<(string, int)>
        {
            ("man",1),("man",9),("pin",1),("pin",9),("sou",1),("sou",9),
            ("wind",1),("wind",2),("wind",3),("wind",4),
            ("dragon",1),("dragon",2),("dragon",3)
        };
        var norm = tiles.Select(t => (TileCategoryHelper.Normalize(t.Category), t.Rank)).ToList();
        return required.IsSubsetOf(new HashSet<(string, int)>(norm));
    }
}
