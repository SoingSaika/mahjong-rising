using System.Collections.Generic;
using System.Linq;

namespace MahjongRising.code.Yaku;

/// <summary>
/// 役种定义注册中心。
/// 管理所有役的数据/显示信息。与 YakuEvaluatorRegistry（判定逻辑）分离。
/// Mod 在注册新役的判定逻辑同时，也在这里注册对应的 YakuDefinition。
/// </summary>
public sealed class YakuDefinitionRegistry
{
    private readonly Dictionary<string, YakuDefinition> _definitions = new();

    public void Register(YakuDefinition def)
    {
        _definitions[def.YakuId] = def;
    }

    public bool Unregister(string yakuId)
    {
        return _definitions.Remove(yakuId);
    }

    public YakuDefinition? Get(string yakuId)
    {
        _definitions.TryGetValue(yakuId, out var def);
        return def;
    }

    public IReadOnlyList<YakuDefinition> GetAll()
        => _definitions.Values.ToList();

    public IReadOnlyList<YakuDefinition> GetByCategory(string category)
        => _definitions.Values.Where(d => d.Category == category).ToList();

    public IReadOnlyList<YakuDefinition> GetAllYakuman()
        => _definitions.Values.Where(d => d.IsYakuman).ToList();

    /// <summary>
    /// 注册所有标准日麻役种定义。
    /// </summary>
    public void RegisterAllBuiltinDefinitions()
    {
        // ═══ 1 番 ═══

        Register(new YakuDefinition {
            YakuId = "riichi", NameJp = "立直", NameRomaji = "Riichi", NameCn = "立直", NameEn = "Riichi",
            HanClosed = 1, HanOpen = -1, Category = "basic",
            DescriptionEn = "Declare Riichi with a ready hand while concealed"
        });
        Register(new YakuDefinition {
            YakuId = "ippatsu", NameJp = "一発", NameRomaji = "Ippatsu", NameCn = "一发", NameEn = "Ippatsu",
            HanClosed = 1, HanOpen = -1, Category = "basic",
            DescriptionEn = "Win within one turn cycle after declaring Riichi"
        });
        Register(new YakuDefinition {
            YakuId = "menzen_tsumo", NameJp = "門前清自摸和", NameRomaji = "Menzen Tsumo", NameCn = "门前清自摸和", NameEn = "Self-draw",
            HanClosed = 1, HanOpen = -1, Category = "basic",
            DescriptionEn = "Win by self-draw with a fully concealed hand"
        });
        Register(new YakuDefinition {
            YakuId = "tanyao", NameJp = "断幺九", NameRomaji = "Tanyao", NameCn = "断幺九", NameEn = "All Simples",
            HanClosed = 1, HanOpen = 1, Category = "basic",
            DescriptionEn = "Hand contains only tiles 2-8 with no terminals or honors"
        });
        Register(new YakuDefinition {
            YakuId = "pinfu", NameJp = "平和", NameRomaji = "Pinfu", NameCn = "平和", NameEn = "Pinfu",
            HanClosed = 1, HanOpen = -1, Category = "basic",
            DescriptionEn = "All sequence melds, non-yakuhai pair, two-sided wait"
        });
        Register(new YakuDefinition {
            YakuId = "iipeiko", NameJp = "一盃口", NameRomaji = "Iipeiko", NameCn = "一杯口", NameEn = "Pure Double Sequence",
            HanClosed = 1, HanOpen = -1, Category = "sequence",
            DescriptionEn = "Two identical sequences of the same suit"
        });
        Register(new YakuDefinition {
            YakuId = "yakuhai_haku", NameJp = "役牌 白", NameRomaji = "Yakuhai Haku", NameCn = "役牌 白", NameEn = "White Dragon",
            HanClosed = 1, HanOpen = 1, Category = "yakuhai",
            DescriptionEn = "Triplet/quad of white dragon (白)"
        });
        Register(new YakuDefinition {
            YakuId = "yakuhai_hatsu", NameJp = "役牌 發", NameRomaji = "Yakuhai Hatsu", NameCn = "役牌 发", NameEn = "Green Dragon",
            HanClosed = 1, HanOpen = 1, Category = "yakuhai",
            DescriptionEn = "Triplet/quad of green dragon (發)"
        });
        Register(new YakuDefinition {
            YakuId = "yakuhai_chun", NameJp = "役牌 中", NameRomaji = "Yakuhai Chun", NameCn = "役牌 中", NameEn = "Red Dragon",
            HanClosed = 1, HanOpen = 1, Category = "yakuhai",
            DescriptionEn = "Triplet/quad of red dragon (中)"
        });
        Register(new YakuDefinition {
            YakuId = "yakuhai_bakaze", NameJp = "場風牌", NameRomaji = "Bakaze", NameCn = "场风", NameEn = "Round Wind",
            HanClosed = 1, HanOpen = 1, Category = "yakuhai",
            DescriptionEn = "Triplet/quad of the prevailing round wind"
        });
        Register(new YakuDefinition {
            YakuId = "yakuhai_jikaze", NameJp = "自風牌", NameRomaji = "Jikaze", NameCn = "自风", NameEn = "Seat Wind",
            HanClosed = 1, HanOpen = 1, Category = "yakuhai",
            DescriptionEn = "Triplet/quad of the player's seat wind"
        });
        Register(new YakuDefinition {
            YakuId = "rinshan_kaihou", NameJp = "嶺上開花", NameRomaji = "Rinshan Kaihou", NameCn = "岭上开花", NameEn = "After a Kan",
            HanClosed = 1, HanOpen = 1, Category = "special",
            DescriptionEn = "Win on the replacement tile drawn after declaring a Kan"
        });
        Register(new YakuDefinition {
            YakuId = "chankan", NameJp = "搶槓", NameRomaji = "Chankan", NameCn = "抢杠", NameEn = "Robbing a Kan",
            HanClosed = 1, HanOpen = 1, Category = "special",
            DescriptionEn = "Win on a tile someone uses for an added Kan"
        });
        Register(new YakuDefinition {
            YakuId = "haitei", NameJp = "海底摸月", NameRomaji = "Haitei Raoyue", NameCn = "海底摸月", NameEn = "Last Tile Draw",
            HanClosed = 1, HanOpen = 1, Category = "special",
            DescriptionEn = "Win by self-draw on the very last tile from the wall"
        });
        Register(new YakuDefinition {
            YakuId = "houtei", NameJp = "河底撈魚", NameRomaji = "Houtei Raoyui", NameCn = "河底捞鱼", NameEn = "Last Tile Discard",
            HanClosed = 1, HanOpen = 1, Category = "special",
            DescriptionEn = "Win on the very last discard of the round"
        });

        // ═══ 2 番 ═══

        Register(new YakuDefinition {
            YakuId = "double_riichi", NameJp = "ダブル立直", NameRomaji = "Double Riichi", NameCn = "双立直", NameEn = "Double Riichi",
            HanClosed = 2, HanOpen = -1, Category = "basic",
            DescriptionEn = "Declare Riichi on your very first discard before any calls"
        });
        Register(new YakuDefinition {
            YakuId = "chiitoitsu", NameJp = "七対子", NameRomaji = "Chiitoitsu", NameCn = "七对子", NameEn = "Seven Pairs",
            HanClosed = 2, HanOpen = -1, Category = "basic",
            DescriptionEn = "Hand consists of seven different pairs"
        });
        Register(new YakuDefinition {
            YakuId = "toitoi", NameJp = "対々和", NameRomaji = "Toitoi", NameCn = "对对和", NameEn = "All Triplets",
            HanClosed = 2, HanOpen = 2, Category = "basic",
            DescriptionEn = "All four melds are triplets/quads"
        });
        Register(new YakuDefinition {
            YakuId = "san_ankou", NameJp = "三暗刻", NameRomaji = "San Ankou", NameCn = "三暗刻", NameEn = "Three Concealed Triplets",
            HanClosed = 2, HanOpen = 2, Category = "basic",
            DescriptionEn = "Hand contains three concealed triplets"
        });
        Register(new YakuDefinition {
            YakuId = "san_shoku_doukou", NameJp = "三色同刻", NameRomaji = "San Shoku Doukou", NameCn = "三色同刻", NameEn = "Triple Triplets",
            HanClosed = 2, HanOpen = 2, Category = "basic",
            DescriptionEn = "Three triplets of the same number in all three suits"
        });
        Register(new YakuDefinition {
            YakuId = "san_kantsu", NameJp = "三槓子", NameRomaji = "San Kantsu", NameCn = "三杠子", NameEn = "Three Kans",
            HanClosed = 2, HanOpen = 2, Category = "basic",
            DescriptionEn = "Hand contains three Kan (quad) melds"
        });
        Register(new YakuDefinition {
            YakuId = "honroutou", NameJp = "混老頭", NameRomaji = "Honroutou", NameCn = "混老头", NameEn = "All Terminals and Honors",
            HanClosed = 2, HanOpen = 2, Category = "terminal",
            DescriptionEn = "All tiles are terminals (1, 9) or honors"
        });
        Register(new YakuDefinition {
            YakuId = "shou_sangen", NameJp = "小三元", NameRomaji = "Shou Sangen", NameCn = "小三元", NameEn = "Little Three Dragons",
            HanClosed = 2, HanOpen = 2, Category = "honor",
            DescriptionEn = "Two dragon triplets and a dragon pair"
        });

        // ═══ 2 番（门前）/ 1 番（副露） ═══

        Register(new YakuDefinition {
            YakuId = "san_shoku", NameJp = "三色同順", NameRomaji = "San Shoku Doujun", NameCn = "三色同顺", NameEn = "Mixed Triple Sequence",
            HanClosed = 2, HanOpen = 1, Category = "sequence",
            DescriptionEn = "Same sequence in all three suits (e.g., 1-2-3 man/pin/sou)"
        });
        Register(new YakuDefinition {
            YakuId = "ittsu", NameJp = "一気通貫", NameRomaji = "Ikkitsuukan", NameCn = "一气通贯", NameEn = "Straight",
            HanClosed = 2, HanOpen = 1, Category = "sequence",
            DescriptionEn = "1-2-3, 4-5-6, 7-8-9 sequences of the same suit"
        });
        Register(new YakuDefinition {
            YakuId = "chanta", NameJp = "混全帯幺九", NameRomaji = "Chanta", NameCn = "混全带幺九", NameEn = "Half Outside Hand",
            HanClosed = 2, HanOpen = 1, Category = "terminal",
            DescriptionEn = "Every meld and the pair contains a terminal or honor"
        });

        // ═══ 3 番 ═══

        Register(new YakuDefinition {
            YakuId = "ryanpeikou", NameJp = "二盃口", NameRomaji = "Ryanpeikou", NameCn = "二杯口", NameEn = "Twice Pure Double Sequence",
            HanClosed = 3, HanOpen = -1, Category = "sequence",
            DescriptionEn = "Two sets of identical sequences"
        });
        Register(new YakuDefinition {
            YakuId = "junchan", NameJp = "純全帯幺九", NameRomaji = "Junchan", NameCn = "纯全带幺九", NameEn = "Fully Outside Hand",
            HanClosed = 3, HanOpen = 2, Category = "terminal",
            DescriptionEn = "Every meld and pair contains a terminal (no honors)"
        });
        Register(new YakuDefinition {
            YakuId = "honitsu", NameJp = "混一色", NameRomaji = "Honitsu", NameCn = "混一色", NameEn = "Half Flush",
            HanClosed = 3, HanOpen = 2, Category = "flush",
            DescriptionEn = "One suit plus honors only"
        });

        // ═══ 6 番 ═══

        Register(new YakuDefinition {
            YakuId = "chinitsu", NameJp = "清一色", NameRomaji = "Chinitsu", NameCn = "清一色", NameEn = "Full Flush",
            HanClosed = 6, HanOpen = 5, Category = "flush",
            DescriptionEn = "Entire hand is a single suit with no honors"
        });

        // ═══ 役满 ═══

        Register(new YakuDefinition {
            YakuId = "kokushi", NameJp = "国士無双", NameRomaji = "Kokushi Musou", NameCn = "国士无双", NameEn = "Thirteen Orphans",
            HanClosed = 13, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "One of each terminal and honor plus one duplicate"
        });
        Register(new YakuDefinition {
            YakuId = "kokushi_13", NameJp = "国士無双十三面", NameRomaji = "Kokushi Juusanmen", NameCn = "国士无双十三面", NameEn = "Thirteen Orphans 13-wait",
            HanClosed = 26, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 2, Category = "yakuman",
            DescriptionEn = "Kokushi with a 13-sided wait (double yakuman)"
        });
        Register(new YakuDefinition {
            YakuId = "suu_ankou", NameJp = "四暗刻", NameRomaji = "Suu Ankou", NameCn = "四暗刻", NameEn = "Four Concealed Triplets",
            HanClosed = 13, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Four concealed triplets"
        });
        Register(new YakuDefinition {
            YakuId = "suu_ankou_tanki", NameJp = "四暗刻単騎", NameRomaji = "Suu Ankou Tanki", NameCn = "四暗刻单骑", NameEn = "Four Concealed Triplets Single Wait",
            HanClosed = 26, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 2, Category = "yakuman",
            DescriptionEn = "Four concealed triplets with a pair wait (double yakuman)"
        });
        Register(new YakuDefinition {
            YakuId = "dai_sangen", NameJp = "大三元", NameRomaji = "Dai Sangen", NameCn = "大三元", NameEn = "Big Three Dragons",
            HanClosed = 13, HanOpen = 13, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Triplets/quads of all three dragons"
        });
        Register(new YakuDefinition {
            YakuId = "shou_suushii", NameJp = "小四喜", NameRomaji = "Shou Suushii", NameCn = "小四喜", NameEn = "Little Four Winds",
            HanClosed = 13, HanOpen = 13, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Three wind triplets and a wind pair"
        });
        Register(new YakuDefinition {
            YakuId = "dai_suushii", NameJp = "大四喜", NameRomaji = "Dai Suushii", NameCn = "大四喜", NameEn = "Big Four Winds",
            HanClosed = 26, HanOpen = 26, IsYakuman = true, YakumanMultiplier = 2, Category = "yakuman",
            DescriptionEn = "Triplets/quads of all four winds (double yakuman)"
        });
        Register(new YakuDefinition {
            YakuId = "tsuuiisou", NameJp = "字一色", NameRomaji = "Tsuuiisou", NameCn = "字一色", NameEn = "All Honors",
            HanClosed = 13, HanOpen = 13, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Entire hand consists of honor tiles only"
        });
        Register(new YakuDefinition {
            YakuId = "chinroutou", NameJp = "清老頭", NameRomaji = "Chinroutou", NameCn = "清老头", NameEn = "All Terminals",
            HanClosed = 13, HanOpen = 13, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Entire hand consists of terminal tiles (1s and 9s) only"
        });
        Register(new YakuDefinition {
            YakuId = "ryuuiisou", NameJp = "緑一色", NameRomaji = "Ryuuiisou", NameCn = "绿一色", NameEn = "All Green",
            HanClosed = 13, HanOpen = 13, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Hand uses only green tiles: 2/3/4/6/8 sou and hatsu"
        });
        Register(new YakuDefinition {
            YakuId = "chuuren_poutou", NameJp = "九蓮宝燈", NameRomaji = "Chuuren Poutou", NameCn = "九莲宝灯", NameEn = "Nine Gates",
            HanClosed = 13, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "1-1-1-2-3-4-5-6-7-8-9-9-9 of one suit plus any tile of that suit"
        });
        Register(new YakuDefinition {
            YakuId = "junsei_chuuren", NameJp = "純正九蓮宝燈", NameRomaji = "Junsei Chuuren Poutou", NameCn = "纯正九莲宝灯", NameEn = "Pure Nine Gates",
            HanClosed = 26, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 2, Category = "yakuman",
            DescriptionEn = "Nine Gates with a 9-sided wait (double yakuman)"
        });
        Register(new YakuDefinition {
            YakuId = "suu_kantsu", NameJp = "四槓子", NameRomaji = "Suu Kantsu", NameCn = "四杠子", NameEn = "Four Kans",
            HanClosed = 13, HanOpen = 13, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Hand contains four Kan (quad) melds"
        });
        Register(new YakuDefinition {
            YakuId = "tenhou", NameJp = "天和", NameRomaji = "Tenhou", NameCn = "天和", NameEn = "Blessing of Heaven",
            HanClosed = 13, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Dealer wins on their initial hand"
        });
        Register(new YakuDefinition {
            YakuId = "chiihou", NameJp = "地和", NameRomaji = "Chiihou", NameCn = "地和", NameEn = "Blessing of Earth",
            HanClosed = 13, HanOpen = -1, IsYakuman = true, YakumanMultiplier = 1, Category = "yakuman",
            DescriptionEn = "Non-dealer wins on their first draw before any calls"
        });

        // ═══ 特殊（宝牌系） ═══

        Register(new YakuDefinition {
            YakuId = "dora", NameJp = "ドラ", NameRomaji = "Dora", NameCn = "宝牌", NameEn = "Dora",
            HanClosed = 1, HanOpen = 1, Category = "special",
            DescriptionEn = "Each dora indicator bonus tile adds 1 han"
        });
        Register(new YakuDefinition {
            YakuId = "uradora", NameJp = "裏ドラ", NameRomaji = "Uradora", NameCn = "里宝牌", NameEn = "Ura-dora",
            HanClosed = 1, HanOpen = -1, Category = "special",
            DescriptionEn = "Under-dora bonus revealed after winning with Riichi"
        });
        Register(new YakuDefinition {
            YakuId = "akadora", NameJp = "赤ドラ", NameRomaji = "Akadora", NameCn = "赤宝牌", NameEn = "Red Dora",
            HanClosed = 1, HanOpen = 1, Category = "special",
            DescriptionEn = "Each red five tile adds 1 han"
        });
    }
}
