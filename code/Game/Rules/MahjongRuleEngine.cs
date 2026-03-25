using System.Collections.Generic;
using System.Threading.Tasks;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.Actions;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.Rules;

/// <summary>
/// 麻将规则引擎主入口。
/// 整合 PlayerAction 执行、Validator 查询、以及可用动作判定。
/// </summary>
public partial class MahjongRuleEngine
{
    private readonly PlayerActionHandlerRegistry _actionHandlerRegistry;
    public ValidatorRegistry Validators { get; }
    public IActionPriorityResolver PriorityResolver { get; }

    public MahjongRuleEngine(
        PlayerActionHandlerRegistry actionHandlerRegistry,
        IActionPriorityResolver priorityResolver,
        ValidatorRegistry validatorRegistry)
    {
        _actionHandlerRegistry = actionHandlerRegistry;
        PriorityResolver = priorityResolver;
        Validators = validatorRegistry;
    }

    // ── 执行动作 ──

    public async Task<MahjongRuleResult> ExecuteAction(
        MahjongGameState gameState,
        PlayerAction action)
    {
        if (!_actionHandlerRegistry.TryGetHandler(action.GetType(), out var handler) || handler == null)
            return MahjongRuleResult.Fail($"没有找到动作处理器：{action.GetType().Name}");

        return await handler.Execute(gameState, action, this);
    }

    // ── 可用动作查询 ──

    /// <summary>
    /// 查询某位玩家对某张牌可以执行的所有动作。
    /// 用于反应窗口打开时告知客户端有哪些选项。
    /// </summary>
    public AvailableActions GetAvailableReactions(
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState targetTile)
    {
        var available = new AvailableActions { Seat = player.Seat };

        // 吃
        var chiResult = Validators.ValidateAll("chi", gameState, player, targetTile);
        if (chiResult.IsValid)
            available.Chi = chiResult.Options;

        // 碰
        var pengResult = Validators.ValidateAll("peng", gameState, player, targetTile);
        if (pengResult.IsValid)
            available.Peng = pengResult.Options;

        // 明杠
        var gangResult = Validators.ValidateAll("gang", gameState, player, targetTile);
        if (gangResult.IsValid)
            available.Gang = gangResult.Options;

        // 荣和
        var huResult = Validators.ValidateAll("hu", gameState, player, targetTile);
        if (huResult.IsValid)
            available.Hu = huResult.Options;

        // Mod 自定义动作
        foreach (var actionType in Validators.GetRegisteredActionTypes())
        {
            if (actionType is "chi" or "peng" or "gang" or "hu") continue;

            var result = Validators.ValidateAll(actionType, gameState, player, targetTile);
            if (result.IsValid)
                available.Custom[actionType] = result.Options;
        }

        return available;
    }

    /// <summary>
    /// 查询当前玩家自摸后可执行的动作（暗杠/加杠/立直/自摸胡）。
    /// </summary>
    public AvailableActions GetAvailableSelfActions(
        MahjongGameState gameState,
        PlayerState player,
        MahjongTileState drawnTile)
    {
        var available = new AvailableActions { Seat = player.Seat };

        // 暗杠/加杠
        var gangResult = Validators.ValidateAll("gang", gameState, player, drawnTile);
        if (gangResult.IsValid)
            available.Gang = gangResult.Options;

        // 自摸
        var huResult = Validators.ValidateAll("hu", gameState, player, drawnTile);
        if (huResult.IsValid)
            available.Hu = huResult.Options;

        // Mod 自定义
        foreach (var actionType in Validators.GetRegisteredActionTypes())
        {
            if (actionType is "chi" or "peng" or "gang" or "hu") continue;

            var result = Validators.ValidateAll(actionType, gameState, player, drawnTile);
            if (result.IsValid)
                available.Custom[actionType] = result.Options;
        }

        return available;
    }
}

/// <summary>
/// 某位玩家当前可用的所有动作选项。
/// 序列化后发送给客户端。
/// </summary>
public class AvailableActions
{
    public int Seat { get; set; }
    public List<ActionOption>? Chi { get; set; }
    public List<ActionOption>? Peng { get; set; }
    public List<ActionOption>? Gang { get; set; }
    public List<ActionOption>? Hu { get; set; }
    public Dictionary<string, List<ActionOption>> Custom { get; } = new();

    public bool HasAny =>
        (Chi != null && Chi.Count > 0)
        || (Peng != null && Peng.Count > 0)
        || (Gang != null && Gang.Count > 0)
        || (Hu != null && Hu.Count > 0)
        || Custom.Count > 0;
}