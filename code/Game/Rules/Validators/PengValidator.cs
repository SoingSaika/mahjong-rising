using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.Rules.Validators.Builtin;

/// <summary>
/// 标准日麻碰牌验证器。
/// 规则：
///   1. 可碰任何人（非自己）打出的牌
///   2. 手牌中必须有 2 张相同牌（同 Category + 同 Rank）
///   3. 立直中不能碰
/// </summary>
public sealed class PengValidator : ITileActionValidator
{
    public string ValidatorId => "builtin.peng";

    public ValidationResult Validate(
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState targetTile,
        ValidationContext context)
    {
        // 立直中不能碰
        if (player.IsRiichi)
            return ValidationResult.Fail("立直中不能碰牌");

        // 不能碰自己打的牌
        int sourceSeat = gameState.ReactionWindow.SourceSeat ?? -1;
        if (sourceSeat == player.Seat)
            return ValidationResult.Fail("不能碰自己打的牌");

        // 查找手牌中同种牌
        var matches = player.Hand
            .Where(t => !t.IsLocked
                && t.Tile.Category == targetTile.Tile.Category
                && t.Tile.Rank == targetTile.Tile.Rank)
            .ToList();

        if (matches.Count < 2)
            return ValidationResult.Fail("手牌中没有足够的相同牌来碰");

        // 生成选项（通常只有一种，但有赤宝时可能有变体）
        var options = new List<ActionOption>();

        // 基础选项：取前两张
        options.Add(new ActionOption
        {
            OptionId = "peng_default",
            InvolvedTileIds = new List<Guid>
            {
                matches[0].InstanceId,
                matches[1].InstanceId
            }
        });

        // 如果有 3 张且含变体（赤宝），生成替代选项
        if (matches.Count >= 3)
        {
            var withVariant = matches.Where(t => t.Tile.Variants.Count > 0).ToList();
            var withoutVariant = matches.Where(t => t.Tile.Variants.Count == 0).ToList();

            if (withVariant.Count > 0 && withoutVariant.Count >= 1)
            {
                // 包含赤宝的选项
                options.Add(new ActionOption
                {
                    OptionId = "peng_with_variant",
                    InvolvedTileIds = new List<Guid>
                    {
                        withVariant[0].InstanceId,
                        withoutVariant[0].InstanceId
                    }
                });
            }
        }

        return ValidationResult.Pass(options);
    }
}
