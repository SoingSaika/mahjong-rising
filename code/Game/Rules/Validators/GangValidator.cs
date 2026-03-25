using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.Rules.Validators.Builtin;

/// <summary>
/// 标准日麻杠牌验证器，支持三种杠：
///   明杠（MinKan）：他家弃牌 + 手中 3 张
///   暗杠（AnKan）：手中 4 张（自摸后、弃牌前可宣告）
///   加杠（KaKan）：已碰的一组 + 手中摸到第 4 张
/// </summary>
public sealed class GangValidator : ITileActionValidator
{
    public string ValidatorId => "builtin.gang";

    public ValidationResult Validate(
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState targetTile,
        ValidationContext context)
    {
        var options = new List<ActionOption>();

        bool isReactionPhase = gameState.Phase == TurnPhase.ReactionPhase;
        bool isSelfAction = gameState.Phase == TurnPhase.SelfActionPhase;

        if (isReactionPhase)
        {
            // ── 明杠：他家弃牌 + 手中 3 张 ──
            var minkanOptions = TryMinKan(player, targetTile, gameState);
            options.AddRange(minkanOptions);
        }

        if (isSelfAction)
        {
            // ── 暗杠：手中 4 张相同牌 ──
            var ankanOptions = TryAnKan(player);
            options.AddRange(ankanOptions);

            // ── 加杠：已碰一组 + 手中第 4 张 ──
            var kakanOptions = TryKaKan(player);
            options.AddRange(kakanOptions);
        }

        if (options.Count == 0)
            return ValidationResult.Fail("没有可执行的杠操作");

        return ValidationResult.Pass(options);
    }

    private static List<ActionOption> TryMinKan(
        PlayerState player, MahjongTileState targetTile, MahjongGameState gameState)
    {
        var options = new List<ActionOption>();
        int sourceSeat = gameState.ReactionWindow.SourceSeat ?? -1;
        if (sourceSeat == player.Seat)
            return options;

        // 立直中不能明杠
        if (player.IsRiichi)
            return options;

        var matches = player.Hand
            .Where(t => !t.IsLocked
                && t.Tile.Category == targetTile.Tile.Category
                && t.Tile.Rank == targetTile.Tile.Rank)
            .ToList();

        if (matches.Count >= 3)
        {
            options.Add(new ActionOption
            {
                OptionId = "minkan",
                InvolvedTileIds = matches.Take(3).Select(t => t.InstanceId).ToList(),
                Extra = new Dictionary<string, object> { ["kind"] = MeldGroup.KindMinkan }
            });
        }

        return options;
    }

    private static List<ActionOption> TryAnKan(PlayerState player)
    {
        var options = new List<ActionOption>();

        var groups = player.Hand
            .Where(t => !t.IsLocked)
            .GroupBy(t => (t.Tile.Category, t.Tile.Rank))
            .Where(g => g.Count() >= 4);

        foreach (var group in groups)
        {
            var tiles = group.Take(4).ToList();

            // 立直中暗杠的特殊限制：不能改变听牌形
            // （此处简化处理，完整版需要听牌判定配合）
            options.Add(new ActionOption
            {
                OptionId = $"ankan_{group.Key.Category}_{group.Key.Rank}",
                InvolvedTileIds = tiles.Select(t => t.InstanceId).ToList(),
                Extra = new Dictionary<string, object> { ["kind"] = MeldGroup.KindAnkan }
            });
        }

        return options;
    }

    private static List<ActionOption> TryKaKan(PlayerState player)
    {
        var options = new List<ActionOption>();

        foreach (var meld in player.Melds.Where(m => m.Kind == MeldGroup.KindPeng))
        {
            var pengTile = meld.Tiles.FirstOrDefault();
            if (pengTile == null) continue;

            var fourth = player.Hand.FirstOrDefault(t =>
                !t.IsLocked
                && t.Tile.Category == pengTile.Tile.Category
                && t.Tile.Rank == pengTile.Tile.Rank);

            if (fourth != null)
            {
                options.Add(new ActionOption
                {
                    OptionId = $"kakan_{pengTile.Tile.Category}_{pengTile.Tile.Rank}",
                    InvolvedTileIds = new List<Guid> { fourth.InstanceId },
                    Extra = new Dictionary<string, object> { ["kind"] = MeldGroup.KindKakan }
                });
            }
        }

        return options;
    }
}
