using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Yaku.Builtin;

// ═══════════════════════════════════════════════════════
// 基类：简化 IYakuRule 实现
// ═══════════════════════════════════════════════════════

/// <summary>
/// 简单役种规则基类。
/// 子类只需实现 Check() 返回 bool。番数从 Definition 中获取。
/// </summary>
public abstract class SimpleYakuRule : IYakuRule
{
    public abstract string YakuId { get; }
    public abstract YakuDefinition Definition { get; }

    public YakuResult? Evaluate(YakuEvalContext ctx)
    {
        if (!Check(ctx)) return null;

        var def = Definition;
        int han = ctx.IsMenzen ? def.HanClosed : def.HanOpen;
        if (han < 0) return null; // 门前限定 + 非门前 → 不成立

        return new YakuResult
        {
            YakuId = YakuId,
            Name = def.NameJp,
            Han = han,
            IsYakuman = def.IsYakuman
        };
    }

    protected abstract bool Check(YakuEvalContext ctx);
}

// ═══════════════════════════════════════════════════════
// 1 番：状况役
// ═══════════════════════════════════════════════════════

public sealed class RiichiRule : SimpleYakuRule
{
    public override string YakuId => "riichi";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.Player.IsRiichi && !ctx.Player.IsDoubleRiichi;
}

public sealed class DoubleRiichiRule : SimpleYakuRule
{
    public override string YakuId => "double_riichi";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.Player.IsDoubleRiichi;
}

public sealed class IppatsuRule : SimpleYakuRule
{
    public override string YakuId => "ippatsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.Player.IsRiichi && ctx.Player.IsIppatsu;
}

public sealed class MenzenTsumoRule : SimpleYakuRule
{
    public override string YakuId => "menzen_tsumo";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.IsTsumo && ctx.IsMenzen;
}

public sealed class RinshanRule : SimpleYakuRule
{
    public override string YakuId => "rinshan_kaihou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.GameState.Phase == TurnPhase.RinShanPhase && ctx.IsTsumo;
}

public sealed class ChankanRule : SimpleYakuRule
{
    public override string YakuId => "chankan";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.GameState.RuntimeTags.Contains("chankan_flag");
}

public sealed class HaiteiRule : SimpleYakuRule
{
    public override string YakuId => "haitei";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.GameState.Wall.Count == 0 && ctx.IsTsumo;
}

public sealed class HouteiRule : SimpleYakuRule
{
    public override string YakuId => "houtei";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.GameState.Wall.Count == 0 && !ctx.IsTsumo;
}

// ═══════════════════════════════════════════════════════
// 1 番：手牌構成役
// ═══════════════════════════════════════════════════════

public sealed class TanyaoRule : SimpleYakuRule
{
    public override string YakuId => "tanyao";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.AllTiles.All(t => H.IsNum(t) && t.Rank >= 2 && t.Rank <= 8);
}

public sealed class PinfuRule : SimpleYakuRule
{
    public override string YakuId => "pinfu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        if (!ctx.IsMenzen || !ctx.HandInfo.IsValid || ctx.HandInfo.Pair == null) return false;
        if (!ctx.HandInfo.Mentsu.All(m => m.Type == "seq")) return false;
        var (pCat, pRank) = ctx.HandInfo.Pair.Value;
        if (pCat is "dragon") return false;
        if (pCat is "wind" && (pRank == ctx.RoundWind + 1 || pRank == ctx.SeatWind + 1)) return false;
        return true;
    }
}

public sealed class IipeikoRule : SimpleYakuRule
{
    public override string YakuId => "iipeiko";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.IsMenzen && ctx.HandInfo.IsValid && ctx.HandInfo.CountDoubleSequences() == 1;
}

// ── 役牌（6 个） ──

public sealed class YakuhaiHakuRule : SimpleYakuRule
{
    public override string YakuId => "yakuhai_haku";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => H.HasTriplet(ctx, "dragon", 1);
}

public sealed class YakuhaiHatsuRule : SimpleYakuRule
{
    public override string YakuId => "yakuhai_hatsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => H.HasTriplet(ctx, "dragon", 2);
}

public sealed class YakuhaiChunRule : SimpleYakuRule
{
    public override string YakuId => "yakuhai_chun";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => H.HasTriplet(ctx, "dragon", 3);
}

public sealed class YakuhaiBakazeRule : SimpleYakuRule
{
    public override string YakuId => "yakuhai_bakaze";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => H.HasTriplet(ctx, "wind", ctx.RoundWind + 1);
}

public sealed class YakuhaiJikazeRule : SimpleYakuRule
{
    public override string YakuId => "yakuhai_jikaze";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => H.HasTriplet(ctx, "wind", ctx.SeatWind + 1);
}

// ═══════════════════════════════════════════════════════
// 2 番
// ═══════════════════════════════════════════════════════

public sealed class ChiitoitsuRule : SimpleYakuRule
{
    public override string YakuId => "chiitoitsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        if (!ctx.IsMenzen || ctx.HandTiles.Count != 14) return false;
        var groups = ctx.HandTiles.GroupBy(t => (H.Norm(t.Category), t.Rank)).ToList();
        return groups.Count == 7 && groups.All(g => g.Count() == 2);
    }
}

public sealed class ToitoiRule : SimpleYakuRule
{
    public override string YakuId => "toitoi";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.HandInfo.IsValid && ctx.HandInfo.AllTriplets();
}

public sealed class SanAnkouRule : SimpleYakuRule
{
    public override string YakuId => "san_ankou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        if (!ctx.HandInfo.IsValid) return false;
        int c = ctx.HandInfo.ConcealedTripletCount(ctx.IsTsumo);
        return c == 3;
    }
}

public sealed class SanShokuDoujunRule : SimpleYakuRule
{
    public override string YakuId => "san_shoku";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.HandInfo.IsValid && ctx.HandInfo.HasSanShokuDoujun();
}

public sealed class SanShokuDoukouRule : SimpleYakuRule
{
    public override string YakuId => "san_shoku_doukou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.HandInfo.IsValid && ctx.HandInfo.HasSanShokuDoukou();
}

public sealed class IttsuRule : SimpleYakuRule
{
    public override string YakuId => "ittsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.HandInfo.IsValid && ctx.HandInfo.HasIttsu();
}

public sealed class ChantaRule : SimpleYakuRule
{
    public override string YakuId => "chanta";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.HandInfo.IsValid && ctx.HandInfo.IsChanta() && !ctx.HandInfo.IsJunchan();
}

public sealed class SanKantsuRule : SimpleYakuRule
{
    public override string YakuId => "san_kantsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.Player.Melds.Count(m => m.Kind is MeldGroup.KindMinkan or MeldGroup.KindAnkan or MeldGroup.KindKakan) == 3;
}

public sealed class HonroutouRule : SimpleYakuRule
{
    public override string YakuId => "honroutou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.AllTiles.All(t => H.IsTerminal(t) || H.IsHonor(t));
}

public sealed class ShouSangenRule : SimpleYakuRule
{
    public override string YakuId => "shou_sangen";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.HandInfo.IsValid && ctx.HandInfo.IsShouSangen();
}

// ═══════════════════════════════════════════════════════
// 3 番
// ═══════════════════════════════════════════════════════

public sealed class RyanpeikoRule : SimpleYakuRule
{
    public override string YakuId => "ryanpeikou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.IsMenzen && ctx.HandInfo.IsValid && ctx.HandInfo.CountDoubleSequences() >= 2;
}

public sealed class JunchanRule : SimpleYakuRule
{
    public override string YakuId => "junchan";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.HandInfo.IsValid && ctx.HandInfo.IsJunchan();
}

public sealed class HonitsuRule : SimpleYakuRule
{
    public override string YakuId => "honitsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        var nums = ctx.AllTiles.Where(t => H.IsNum(t)).ToList();
        if (nums.Count == 0 || nums.Count == ctx.AllTiles.Count) return false; // 无数牌=字一色，全数牌=清一色
        string suit = H.Norm(nums[0].Category);
        return nums.All(t => H.Norm(t.Category) == suit);
    }
}

// ═══════════════════════════════════════════════════════
// 6 番
// ═══════════════════════════════════════════════════════

public sealed class ChinitsuRule : SimpleYakuRule
{
    public override string YakuId => "chinitsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        if (ctx.AllTiles.Any(t => H.IsHonor(t))) return false;
        if (ctx.AllTiles.Count == 0) return false;
        string suit = H.Norm(ctx.AllTiles[0].Category);
        return ctx.AllTiles.All(t => H.Norm(t.Category) == suit);
    }
}

// ═══════════════════════════════════════════════════════
// 役満
// ═══════════════════════════════════════════════════════

public sealed class KokushiRule : SimpleYakuRule
{
    public override string YakuId => "kokushi";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        if (!ctx.IsMenzen || ctx.HandTiles.Count != 14) return false;
        var norm = ctx.HandTiles.Select(t => (H.Norm(t.Category), t.Rank)).ToList();
        return H.KokushiSet.IsSubsetOf(new HashSet<(string, int)>(norm));
    }
}

public sealed class SuuAnkouRule : SimpleYakuRule
{
    public override string YakuId => "suu_ankou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.IsMenzen && ctx.HandInfo.IsValid && ctx.HandInfo.ConcealedTripletCount(ctx.IsTsumo) >= 4;
}

public sealed class DaiSangenRule : SimpleYakuRule
{
    public override string YakuId => "dai_sangen";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        int dragonTri = 0;
        for (int r = 1; r <= 3; r++)
            if (ctx.AllTiles.Count(t => H.Norm(t.Category) == "dragon" && t.Rank == r) >= 3) dragonTri++;
        return dragonTri >= 3;
    }
}

public sealed class ShouSuushiiRule : SimpleYakuRule
{
    public override string YakuId => "shou_suushii";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => H.WindCheck(ctx) == 1;
}

public sealed class DaiSuushiiRule : SimpleYakuRule
{
    public override string YakuId => "dai_suushii";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => H.WindCheck(ctx) == 2;
}

public sealed class TsuuiisouRule : SimpleYakuRule
{
    public override string YakuId => "tsuuiisou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx) => ctx.AllTiles.All(t => H.IsHonor(t));
}

public sealed class ChinroutouRule : SimpleYakuRule
{
    public override string YakuId => "chinroutou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.AllTiles.All(t => H.IsTerminal(t) && !H.IsHonor(t));
}

public sealed class RyuuiisouRule : SimpleYakuRule
{
    public override string YakuId => "ryuuiisou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.AllTiles.All(t =>
        {
            var c = H.Norm(t.Category);
            if (c == "dragon") return t.Rank == 2;
            if (c == "sou") return t.Rank is 2 or 3 or 4 or 6 or 8;
            return false;
        });
}

public sealed class ChuurenRule : SimpleYakuRule
{
    public override string YakuId => "chuuren_poutou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
    {
        if (!ctx.IsMenzen || ctx.HandTiles.Count != 14) return false;
        var cats = ctx.HandTiles.Select(t => H.Norm(t.Category)).Distinct().ToList();
        if (cats.Count != 1 || !H.IsNumCat(cats[0])) return false;
        var rc = new int[10];
        foreach (var t in ctx.HandTiles) rc[t.Rank]++;
        int[] req = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 3 };
        for (int i = 1; i <= 9; i++) if (rc[i] < req[i]) return false;
        return true;
    }
}

public sealed class SuuKantsuRule : SimpleYakuRule
{
    public override string YakuId => "suu_kantsu";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.Player.Melds.Count(m => m.Kind is MeldGroup.KindMinkan or MeldGroup.KindAnkan or MeldGroup.KindKakan) >= 4;
}

public sealed class TenhouRule : SimpleYakuRule
{
    public override string YakuId => "tenhou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.IsTsumo && ctx.GameState.TurnCount == 0
        && ctx.Player.Seat == ctx.GameState.DealerSeat
        && ctx.GameState.Players.All(p => p.Melds.Count == 0);
}

public sealed class ChiihouRule : SimpleYakuRule
{
    public override string YakuId => "chiihou";
    public override YakuDefinition Definition => Defs.Get(YakuId);
    protected override bool Check(YakuEvalContext ctx)
        => ctx.IsTsumo && ctx.GameState.TurnCount <= 1
        && ctx.Player.Seat != ctx.GameState.DealerSeat
        && ctx.GameState.Players.All(p => p.Melds.Count == 0);
}

// ═══════════════════════════════════════════════════════
// 宝牌系（IMultiYakuRule — 可返回多个结果）
// ═══════════════════════════════════════════════════════

public sealed class DoraRule : IMultiYakuRule
{
    public string YakuId => "dora";
    public YakuDefinition Definition => Defs.Get(YakuId);

    public YakuResult? Evaluate(YakuEvalContext ctx)
    {
        var all = EvaluateAll(ctx);
        return all.Count > 0 ? all[0] : null;
    }

    public List<YakuResult> EvaluateAll(YakuEvalContext ctx)
    {
        int count = ctx.GameState.Counters.GetValueOrDefault("dora_count", 0);
        var results = new List<YakuResult>();
        for (int i = 0; i < count; i++)
            results.Add(new YakuResult { YakuId = "dora", Name = "ドラ", Han = 1 });
        return results;
    }
}

public sealed class AkadoraRule : IMultiYakuRule
{
    public string YakuId => "akadora";
    public YakuDefinition Definition => Defs.Get(YakuId);

    public YakuResult? Evaluate(YakuEvalContext ctx)
    {
        var all = EvaluateAll(ctx);
        return all.Count > 0 ? all[0] : null;
    }

    public List<YakuResult> EvaluateAll(YakuEvalContext ctx)
    {
        int count = ctx.HandTiles.Count(t => t.Variants.Contains("red"));
        var results = new List<YakuResult>();
        for (int i = 0; i < count; i++)
            results.Add(new YakuResult { YakuId = "akadora", Name = "赤ドラ", Han = 1 });
        return results;
    }
}

// ═══════════════════════════════════════════════════════
// 共享辅助工具
// ═══════════════════════════════════════════════════════

/// <summary>判定辅助快捷方法。</summary>
internal static class H
{
    internal static string Norm(string cat) => TileCategoryHelper.Normalize(cat);
    internal static bool IsNum(Mahjong.Tiles.MahjongTile t) => TileCategoryHelper.IsNumber(Norm(t.Category));
    internal static bool IsNumCat(string cat) => TileCategoryHelper.IsNumber(cat);
    internal static bool IsHonor(Mahjong.Tiles.MahjongTile t) => TileCategoryHelper.IsHonor(Norm(t.Category));
    internal static bool IsTerminal(Mahjong.Tiles.MahjongTile t) => IsNum(t) && (t.Rank == 1 || t.Rank == 9);

    internal static bool HasTriplet(YakuEvalContext ctx, string cat, int rank)
        => ctx.AllTiles.Count(t => Norm(t.Category) == cat && t.Rank == rank) >= 3;

    /// <summary>返回 0=无, 1=小四喜, 2=大四喜</summary>
    internal static int WindCheck(YakuEvalContext ctx)
    {
        int tri = 0; bool pair = false;
        for (int r = 1; r <= 4; r++)
        {
            int c = ctx.AllTiles.Count(t => Norm(t.Category) == "wind" && t.Rank == r);
            if (c >= 3) tri++; else if (c >= 2) pair = true;
        }
        if (tri >= 4) return 2;
        if (tri >= 3 && pair) return 1;
        return 0;
    }

    internal static readonly HashSet<(string, int)> KokushiSet = new()
    {
        ("man", 1), ("man", 9), ("pin", 1), ("pin", 9), ("sou", 1), ("sou", 9),
        ("wind", 1), ("wind", 2), ("wind", 3), ("wind", 4),
        ("dragon", 1), ("dragon", 2), ("dragon", 3)
    };
}

/// <summary>
/// YakuDefinition 的缓存查找。
/// 在 GameBootstrap 注册阶段，先调 YakuDefinitionRegistry.RegisterAllBuiltinDefinitions()，
/// 然后 Defs.Init(registry) 缓存引用。
/// </summary>
public static class Defs
{
    private static YakuDefinitionRegistry? _reg;

    public static void Init(YakuDefinitionRegistry registry) => _reg = registry;

    public static YakuDefinition Get(string yakuId)
        => _reg?.Get(yakuId) ?? new YakuDefinition { YakuId = yakuId, NameEn = yakuId, HanClosed = 0 };
}