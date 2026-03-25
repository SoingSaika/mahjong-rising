using System.Collections.Generic;
using System.Linq;

namespace MahjongRising.code.Yaku;

/// <summary>
/// 役种规则统一注册中心。
/// 每个 IYakuRule 自带 Definition（番数 + 名称 + 资源），一步注册完成逻辑和数据。
///
/// Mod 操作：
///   Register(rule)          添加新役
///   Replace(rule)           替换已有役（改番数或逻辑）
///   Unregister(yakuId)      删除一个役
///   SetEnabled(yakuId, b)   启用/禁用（保留定义但跳过判定）
/// </summary>
public sealed class YakuRuleRegistry
{
    private readonly Dictionary<string, IYakuRule> _rules = new();
    private readonly HashSet<string> _disabled = new();

    public void Register(IYakuRule rule) => _rules[rule.YakuId] = rule;
    public void Replace(IYakuRule rule) => _rules[rule.YakuId] = rule;
    public bool Unregister(string yakuId) { _disabled.Remove(yakuId); return _rules.Remove(yakuId); }
    public void SetEnabled(string yakuId, bool enabled) { if (enabled) _disabled.Remove(yakuId); else _disabled.Add(yakuId); }
    public bool IsEnabled(string yakuId) => !_disabled.Contains(yakuId);

    public IReadOnlyList<IYakuRule> GetActiveRules()
        => _rules.Values.Where(r => !_disabled.Contains(r.YakuId)).ToList();

    public IReadOnlyList<IYakuRule> GetAllRules() => _rules.Values.ToList();

    public YakuDefinition? GetDefinition(string yakuId)
        => _rules.TryGetValue(yakuId, out var rule) ? rule.Definition : null;

    public IReadOnlyList<YakuDefinition> GetAllDefinitions()
        => _rules.Values.Select(r => r.Definition).ToList();

    /// <summary>执行所有活跃规则判定。手牌解析在 YakuEvalContext 创建时只做一次。</summary>
    public List<YakuResult> EvaluateAll(YakuEvalContext context)
    {
        var results = new List<YakuResult>();

        foreach (var rule in GetActiveRules())
        {
            if (rule is IMultiYakuRule multi)
                results.AddRange(multi.EvaluateAll(context));
            else
            {
                var r = rule.Evaluate(context);
                if (r != null) results.Add(r);
            }
        }

        if (results.Any(r => r.IsYakuman))
            results = results.Where(r => r.IsYakuman).ToList();

        return results;
    }
}