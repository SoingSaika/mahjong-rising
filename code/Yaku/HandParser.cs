using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Mahjong.Tiles;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Yaku;

/// <summary>
/// 解析后的手牌面子分解信息。
/// 由 HandParser 生成，缓存在 YakuEvalContext 中供所有 IYakuRule 共享。
/// </summary>
public class HandInfo
{
    public bool IsValid { get; init; }
    public (string Cat, int Rank)? Pair { get; init; }
    public List<MentsuInfo> Mentsu { get; init; } = new();

    public bool AllTriplets()
        => IsValid && Mentsu.All(m => m.Type == "tri");

    public int ConcealedTripletCount(bool isTsumo)
        => Mentsu.Count(m => m.Type == "tri" && m.IsConcealed);

    public int CountDoubleSequences()
    {
        var seqs = Mentsu.Where(m => m.Type == "seq").Select(m => (m.Cat, m.StartRank)).ToList();
        return seqs.GroupBy(s => s).Count(g => g.Count() >= 2);
    }

    public bool HasSanShokuDoujun()
    {
        var seqs = Mentsu.Where(m => m.Type == "seq").Select(m => (m.Cat, m.StartRank)).ToList();
        return seqs.GroupBy(s => s.StartRank).Any(g => g.Select(s => s.Cat).Distinct().Count() >= 3);
    }

    public bool HasSanShokuDoukou()
    {
        var tris = Mentsu.Where(m => m.Type == "tri").Select(m => (m.Cat, m.StartRank)).ToList();
        return tris.GroupBy(t => t.StartRank).Any(g =>
            g.Select(t => t.Cat).Where(c => c is "man" or "pin" or "sou").Distinct().Count() >= 3);
    }

    public bool HasIttsu()
    {
        var seqs = Mentsu.Where(m => m.Type == "seq").Select(m => (m.Cat, m.StartRank)).ToList();
        return seqs.GroupBy(s => s.Cat).Any(g =>
        {
            var ranks = g.Select(s => s.StartRank).ToHashSet();
            return ranks.Contains(1) && ranks.Contains(4) && ranks.Contains(7);
        });
    }

    public bool IsChanta()
    {
        if (!IsValid || Pair == null) return false;
        return IsTerminalOrHonor(Pair.Value.Cat, Pair.Value.Rank)
            && Mentsu.All(m => m.HasTerminalOrHonor());
    }

    public bool IsJunchan()
    {
        if (!IsValid || Pair == null) return false;
        if (!IsTerminalOnly(Pair.Value.Cat, Pair.Value.Rank)) return false;
        if (!Mentsu.All(m => m.HasTerminalOnly())) return false;
        bool hasHonor = TileCategoryHelper.IsHonor(Pair.Value.Cat)
            || Mentsu.Any(m => TileCategoryHelper.IsHonor(m.Cat));
        return !hasHonor;
    }

    public bool IsShouSangen()
    {
        if (Pair == null) return false;
        return Pair.Value.Cat == "dragon"
            && Mentsu.Count(m => m.Type == "tri" && m.Cat == "dragon") >= 2;
    }

    public bool IsPinfu(int roundWind, int seatWind)
    {
        if (!IsValid || Pair == null) return false;
        if (!Mentsu.All(m => m.Type == "seq")) return false;
        var (pCat, pRank) = Pair.Value;
        if (pCat == "dragon") return false;
        if (pCat == "wind" && (pRank == roundWind + 1 || pRank == seatWind + 1)) return false;
        return true;
    }

    /// <summary>收集所有刻子/杠子的 (cat, rank)，含手牌和副露。</summary>
    public List<(string Cat, int Rank)> AllGroupedTriplets(List<MeldGroup> melds)
    {
        var result = Mentsu.Where(m => m.Type == "tri").Select(m => (m.Cat, m.StartRank)).ToList();
        foreach (var meld in melds)
        {
            if (meld.Tiles.Count < 3) continue;
            var t = meld.Tiles[0].Tile;
            var key = (TileCategoryHelper.Normalize(t.Category), t.Rank);
            if (!result.Contains(key)) result.Add(key);
        }
        return result;
    }

    private static bool IsTerminalOrHonor(string cat, int rank)
    {
        if (TileCategoryHelper.IsHonor(cat)) return true;
        return TileCategoryHelper.IsNumber(cat) && (rank == 1 || rank == 9);
    }

    private static bool IsTerminalOnly(string cat, int rank)
        => TileCategoryHelper.IsNumber(cat) && (rank == 1 || rank == 9);
}

/// <summary>面子信息。</summary>
public class MentsuInfo
{
    public string Type { get; init; } = "";   // "seq" 顺子 / "tri" 刻子
    public string Cat { get; init; } = "";    // 标准化分类
    public int StartRank { get; init; }
    public bool IsConcealed { get; init; } = true;

    public bool HasTerminalOrHonor()
    {
        if (TileCategoryHelper.IsHonor(Cat)) return true;
        if (Type == "seq") return StartRank == 1 || StartRank + 2 == 9;
        return StartRank == 1 || StartRank == 9;
    }

    public bool HasTerminalOnly()
    {
        if (TileCategoryHelper.IsHonor(Cat)) return false;
        if (Type == "seq") return StartRank == 1 || StartRank + 2 == 9;
        return StartRank == 1 || StartRank == 9;
    }
}

/// <summary>手牌拆面子解析器。</summary>
public static class HandParser
{
    public static HandInfo Parse(List<MahjongTile> handTiles, List<MeldGroup> melds)
    {
        var fixedMentsu = new List<MentsuInfo>();
        foreach (var meld in melds)
        {
            if (meld.Tiles.Count < 3) continue;
            var t = meld.Tiles[0].Tile;
            string cat = TileCategoryHelper.Normalize(t.Category);
            if (meld.Kind == MeldGroup.KindChi)
            {
                int minRank = meld.Tiles.Min(x => x.Tile.Rank);
                fixedMentsu.Add(new MentsuInfo { Type = "seq", Cat = cat, StartRank = minRank, IsConcealed = false });
            }
            else
            {
                fixedMentsu.Add(new MentsuInfo { Type = "tri", Cat = cat, StartRank = t.Rank, IsConcealed = !meld.IsOpen });
            }
        }

        var counts = new Dictionary<(string, int), int>();
        foreach (var t in handTiles)
        {
            var key = (TileCategoryHelper.Normalize(t.Category), t.Rank);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        foreach (var pairKey in counts.Where(kv => kv.Value >= 2).Select(kv => kv.Key).ToList())
        {
            var remaining = new Dictionary<(string, int), int>(counts);
            remaining[pairKey] -= 2;
            if (remaining[pairKey] == 0) remaining.Remove(pairKey);

            var mentsuList = new List<MentsuInfo>();
            if (TryDecompose(remaining, mentsuList))
            {
                mentsuList.AddRange(fixedMentsu);
                return new HandInfo { IsValid = true, Pair = pairKey, Mentsu = mentsuList };
            }
        }

        return new HandInfo { IsValid = false };
    }

    private static bool TryDecompose(Dictionary<(string, int), int> counts, List<MentsuInfo> result)
    {
        if (counts.Count == 0) return true;

        var first = counts.OrderBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item2).First();
        var (cat, rank) = first.Key;

        if (first.Value >= 3)
        {
            var copy = new Dictionary<(string, int), int>(counts);
            copy[(cat, rank)] -= 3;
            if (copy[(cat, rank)] == 0) copy.Remove((cat, rank));
            var branch = new List<MentsuInfo>(result) { new() { Type = "tri", Cat = cat, StartRank = rank, IsConcealed = true } };
            if (TryDecompose(copy, branch)) { result.Clear(); result.AddRange(branch); return true; }
        }

        if (TileCategoryHelper.IsNumber(cat) && rank <= 7
            && counts.GetValueOrDefault((cat, rank + 1)) >= 1
            && counts.GetValueOrDefault((cat, rank + 2)) >= 1)
        {
            var copy = new Dictionary<(string, int), int>(counts);
            copy[(cat, rank)]--; copy[(cat, rank + 1)]--; copy[(cat, rank + 2)]--;
            if (copy[(cat, rank)] == 0) copy.Remove((cat, rank));
            if (copy[(cat, rank + 1)] == 0) copy.Remove((cat, rank + 1));
            if (copy[(cat, rank + 2)] == 0) copy.Remove((cat, rank + 2));
            var branch = new List<MentsuInfo>(result) { new() { Type = "seq", Cat = cat, StartRank = rank, IsConcealed = true } };
            if (TryDecompose(copy, branch)) { result.Clear(); result.AddRange(branch); return true; }
        }

        return false;
    }
}
