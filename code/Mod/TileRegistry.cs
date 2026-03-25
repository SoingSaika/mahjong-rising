using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Mahjong.Tiles;

namespace MahjongRising.code.Mod;

/// <summary>
/// 牌种注册中心。
/// 管理所有牌的定义和工厂函数，Mod 可注册全新牌种。
/// </summary>
public sealed class TileRegistry
{
    // tileCode → 工厂函数
    private readonly Dictionary<string, Func<MahjongTile>> _factories = new();

    // tileCode → 该牌在标准牌组中的数量（如普通牌 4 张，赤宝 1 张）
    private readonly Dictionary<string, int> _counts = new();

    /// <summary>
    /// 注册一种牌。
    /// </summary>
    /// <param name="tileCode">牌编码（如 "man_1", "dragon_red", "mod_curse_1"）</param>
    /// <param name="factory">创建牌实例的工厂函数</param>
    /// <param name="countInSet">在一副牌中的数量</param>
    public void Register(string tileCode, Func<MahjongTile> factory, int countInSet = 4)
    {
        _factories[tileCode] = factory;
        _counts[tileCode] = countInSet;
    }

    /// <summary>取消注册。</summary>
    public bool Unregister(string tileCode)
    {
        _counts.Remove(tileCode);
        return _factories.Remove(tileCode);
    }

    /// <summary>创建单个牌实例。</summary>
    public MahjongTile? Create(string tileCode)
    {
        return _factories.TryGetValue(tileCode, out var factory) ? factory() : null;
    }

    /// <summary>获取所有已注册的 tileCode。</summary>
    public IReadOnlyList<string> GetAllTileCodes() => _factories.Keys.ToList();

    /// <summary>获取某牌在一副中的数量。</summary>
    public int GetCount(string tileCode) => _counts.GetValueOrDefault(tileCode, 0);

    /// <summary>
    /// 生成一副完整牌组（用于洗牌前）。
    /// 返回 (tileCode, MahjongTile) 列表。
    /// </summary>
    public List<MahjongTile> CreateFullSet()
    {
        var set = new List<MahjongTile>();
        foreach (var code in _factories.Keys)
        {
            int count = _counts.GetValueOrDefault(code, 4);
            var factory = _factories[code];
            for (int i = 0; i < count; i++)
            {
                set.Add(factory());
            }
        }
        return set;
    }
}
