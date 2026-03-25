using System;
using System.Collections.Generic;
using System.Linq;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.Rules.Validators;

/// <summary>
/// 验证器注册中心。
/// 按 ActionType 分组管理验证器；同一 ActionType 可注册多个验证器（链式验证）。
/// Mod 在初始化时调用 Register 注入自定义验证器。
/// </summary>
public sealed class ValidatorRegistry
{
    // key = actionType ("chi"/"peng"/"gang"/"hu"/自定义)
    private readonly Dictionary<string, List<ITileActionValidator>> _validators = new();

    /// <summary>注册验证器到指定动作类型。</summary>
    public void Register(string actionType, ITileActionValidator validator)
    {
        if (!_validators.ContainsKey(actionType))
            _validators[actionType] = new List<ITileActionValidator>();

        // 避免重复注册
        if (_validators[actionType].Any(v => v.ValidatorId == validator.ValidatorId))
            return;

        _validators[actionType].Add(validator);
    }

    /// <summary>移除指定验证器（Mod 卸载时用）。</summary>
    public bool Unregister(string actionType, string validatorId)
    {
        if (!_validators.TryGetValue(actionType, out var list))
            return false;
        return list.RemoveAll(v => v.ValidatorId == validatorId) > 0;
    }

    /// <summary>
    /// 对指定动作类型执行所有已注册的验证器。
    /// 全部通过才算通过；合并所有 Options。
    /// </summary>
    public ValidationResult ValidateAll(
        string actionType,
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState targetTile,
        ValidationContext? context = null)
    {
        context ??= new ValidationContext { ActionType = actionType };

        if (!_validators.TryGetValue(actionType, out var list) || list.Count == 0)
            return ValidationResult.Fail($"没有注册 {actionType} 的验证器");

        var allOptions = new List<ActionOption>();

        foreach (var validator in list)
        {
            var result = validator.Validate(gameState, player, targetTile, context);
            if (!result.IsValid)
                return result; // 任一验证器拒绝则失败

            allOptions.AddRange(result.Options);
        }

        return ValidationResult.Pass(allOptions);
    }

    /// <summary>检查某个动作类型是否有任何已注册的验证器。</summary>
    public bool HasValidators(string actionType)
        => _validators.ContainsKey(actionType) && _validators[actionType].Count > 0;

    /// <summary>获取所有已注册的动作类型。</summary>
    public IReadOnlyList<string> GetRegisteredActionTypes()
        => _validators.Keys.ToList();
}
