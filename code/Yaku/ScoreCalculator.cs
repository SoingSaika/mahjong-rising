using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.State;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Yaku;

/// <summary>
/// 日麻得分计算器。
/// 根据 番数(han) + 符数(fu) 计算基本点数，再按庄/闲、自摸/荣和分配。
/// Mod 可继承覆盖任何计算步骤。
/// </summary>
public class ScoreCalculator
{
    /// <summary>
    /// 计算完整得分结果。
    /// </summary>
    public virtual ScoreResult Calculate(
        int han, int fu, bool isDealer, bool isTsumo,
        int riichiSticks, int honba, bool isYakuman = false)
    {
        int basePoints = CalculateBasePoints(han, fu, isYakuman);
        return DistributePoints(basePoints, isDealer, isTsumo, riichiSticks, honba);
    }

    /// <summary>
    /// 计算基本点（基本点 = 符 × 2^(番+2)，上限切入满贯等）。
    /// </summary>
    public virtual int CalculateBasePoints(int han, int fu, bool isYakuman)
    {
        if (isYakuman)
        {
            // 役满倍数由 han/13 决定
            int multiplier = Math.Max(1, han / 13);
            return 8000 * multiplier;
        }

        // 满贯以上直接查表
        if (han >= 13) return 8000;   // 数え役満
        if (han >= 11) return 6000;   // 三倍満
        if (han >= 8)  return 4000;   // 倍満
        if (han >= 6)  return 3000;   // 跳満
        if (han >= 5)  return 2000;   // 満貫

        // 4 番 30 符以上 或 3 番 60 符以上 → 切入满贯
        if (han == 4 && fu >= 30) return 2000;
        if (han == 3 && fu >= 60) return 2000;

        // 基本点 = fu × 2^(han+2)
        int basePoints = fu * (1 << (han + 2));

        // 上限 2000（满贯）
        return Math.Min(basePoints, 2000);
    }

    /// <summary>
    /// 根据基本点分配各家得失分。
    /// </summary>
    public virtual ScoreResult DistributePoints(
        int basePoints, bool isDealer, bool isTsumo,
        int riichiSticks, int honba)
    {
        var result = new ScoreResult();
        int honbaBonus = honba * 300;
        int riichiBonus = riichiSticks * 1000;

        if (isTsumo)
        {
            if (isDealer)
            {
                // 庄家自摸：各闲家付 基本点×2（切上百位）
                int eachPays = CeilTo100(basePoints * 2);
                result.WinnerGain = eachPays * 3 + honbaBonus + riichiBonus;
                result.LoserPayments[-1] = eachPays + (honba * 100); // -1 = 每个闲家
                result.PaymentDescription = $"各家 {eachPays + honba * 100} 点";
            }
            else
            {
                // 闲家自摸：庄家付 基本点×2，闲家付 基本点×1
                int dealerPays = CeilTo100(basePoints * 2);
                int nonDealerPays = CeilTo100(basePoints);
                result.WinnerGain = dealerPays + nonDealerPays * 2 + honbaBonus + riichiBonus;
                result.DealerPayment = dealerPays + (honba * 100);
                result.NonDealerPayment = nonDealerPays + (honba * 100);
                result.PaymentDescription = $"庄 {result.DealerPayment} / 闲 {result.NonDealerPayment}";
            }
        }
        else
        {
            // 荣和：放铳者一人支付
            int totalPay;
            if (isDealer)
                totalPay = CeilTo100(basePoints * 6);
            else
                totalPay = CeilTo100(basePoints * 4);

            totalPay += honbaBonus;
            result.WinnerGain = totalPay + riichiBonus;
            result.RonPayment = totalPay;
            result.PaymentDescription = $"放铳者 {totalPay} 点";
        }

        return result;
    }

    /// <summary>
    /// 计算符数。
    /// </summary>
    public virtual int CalculateFu(YakuEvalContext ctx, List<YakuResult> yakuList)
    {
        // 七对子固定 25 符
        if (yakuList.Any(y => y.YakuId == "chiitoitsu"))
            return 25;

        int fu = 20; // 副底

        // 门前荣和 +10
        if (ctx.IsMenzen && !ctx.IsTsumo) fu += 10;
        // 自摸 +2（平和自摸除外）
        if (ctx.IsTsumo && !yakuList.Any(y => y.YakuId == "pinfu")) fu += 2;

        if (!ctx.HandInfo.IsValid) return CeilFu(fu);

        // 面子符
        foreach (var mentsu in ctx.HandInfo.Mentsu)
        {
            int mentsuFu = GetMentsuFu(mentsu);
            fu += mentsuFu;
        }

        // 副露的面子符
        foreach (var meld in ctx.Player.Melds)
        {
            fu += GetMeldFu(meld);
        }

        // 雀头符
        if (ctx.HandInfo.Pair != null)
        {
            var (cat, rank) = ctx.HandInfo.Pair.Value;
            if (cat is "dragon") fu += 2;
            if (cat is "wind")
            {
                if (rank == ctx.RoundWind + 1) fu += 2;
                if (rank == ctx.SeatWind + 1) fu += 2;
            }
        }

        // 听牌形符（嵌张/边张/单骑 +2，两面/双碰 +0）
        // 简化处理：默认 +0，完整版需要分析听牌形
        // fu += GetWaitFu(ctx);

        return CeilFu(fu);
    }

    private static int GetMentsuFu(MentsuInfo mentsu)
    {
        if (mentsu.Type == "seq") return 0; // 顺子 0 符

        // 刻子基本 2 符
        int baseFu = 2;

        // 暗刻 ×2
        if (mentsu.IsConcealed) baseFu *= 2;

        // 幺九牌 ×2
        bool isTerminalOrHonor = mentsu.Cat is "wind" or "dragon"
            || (mentsu.StartRank == 1 || mentsu.StartRank == 9);
        if (isTerminalOrHonor) baseFu *= 2;

        return baseFu;
    }

    private static int GetMeldFu(MeldGroup meld)
    {
        if (meld.Kind == MeldGroup.KindChi) return 0;

        var tile = meld.Tiles.FirstOrDefault()?.Tile;
        if (tile == null) return 0;

        bool isTerminalOrHonor = TileCategoryHelper.IsHonor(TileCategoryHelper.Normalize(tile.Category))
            || tile.Rank == 1 || tile.Rank == 9;

        int baseFu;
        if (meld.Kind is MeldGroup.KindMinkan)
            baseFu = isTerminalOrHonor ? 16 : 8;      // 明杠
        else if (meld.Kind is MeldGroup.KindAnkan)
            baseFu = isTerminalOrHonor ? 32 : 16;     // 暗杠
        else if (meld.Kind is MeldGroup.KindKakan)
            baseFu = isTerminalOrHonor ? 16 : 8;      // 加杠（同明杠）
        else if (meld.IsOpen)
            baseFu = isTerminalOrHonor ? 4 : 2;       // 明刻
        else
            baseFu = isTerminalOrHonor ? 8 : 4;       // 暗刻

        return baseFu;
    }

    /// <summary>切上到 10 的倍数。</summary>
    private static int CeilFu(int fu)
    {
        return (fu + 9) / 10 * 10;
    }

    /// <summary>切上到 100 的倍数。</summary>
    private static int CeilTo100(int points)
    {
        return (points + 99) / 100 * 100;
    }

    /// <summary>
    /// 计算所有玩家的分数变化。
    /// </summary>
    public Dictionary<int, int> CalculateScoreChanges(
        MahjongGameState gameState, int winnerSeat, int? loserSeat,
        int han, int fu, bool isYakuman, bool isTsumo)
    {
        var winner = gameState.GetPlayer(winnerSeat);
        bool isDealer = winnerSeat == gameState.DealerSeat;

        var scoreResult = Calculate(han, fu, isDealer, isTsumo,
            gameState.RiichiSticks, gameState.Honba, isYakuman);

        var changes = new Dictionary<int, int>();
        changes[winnerSeat] = scoreResult.WinnerGain;

        if (isTsumo)
        {
            for (int i = 0; i < gameState.PlayerCount; i++)
            {
                if (i == winnerSeat) continue;
                bool isPlayerDealer = i == gameState.DealerSeat;

                if (isDealer)
                {
                    // 庄家自摸，各闲家均摊
                    changes[i] = -(scoreResult.LoserPayments.GetValueOrDefault(-1, 0));
                }
                else
                {
                    changes[i] = isPlayerDealer
                        ? -scoreResult.DealerPayment
                        : -scoreResult.NonDealerPayment;
                }
            }
        }
        else if (loserSeat.HasValue)
        {
            // 荣和，放铳者一人付
            changes[loserSeat.Value] = -scoreResult.RonPayment;
        }

        return changes;
    }
}

/// <summary>得分计算结果。</summary>
public class ScoreResult
{
    /// <summary>和了者总得分。</summary>
    public int WinnerGain { get; set; }

    /// <summary>荣和时放铳者的支付额。</summary>
    public int RonPayment { get; set; }

    /// <summary>自摸时庄家支付额（仅闲家自摸时使用）。</summary>
    public int DealerPayment { get; set; }

    /// <summary>自摸时闲家支付额（仅闲家自摸时使用）。</summary>
    public int NonDealerPayment { get; set; }

    /// <summary>庄家自摸时，各闲家支付（key=-1）。</summary>
    public Dictionary<int, int> LoserPayments { get; set; } = new();

    /// <summary>支付描述（用于 UI 展示）。</summary>
    public string PaymentDescription { get; set; } = "";
}