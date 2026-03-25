using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.Actions;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Mahjong.Tiles;
using MahjongRising.code.Mod;
using MahjongRising.code.Player.Actions;
using MahjongRising.code.Player.States;
using MahjongRising.code.Resources;
using MahjongRising.code.Yaku;

namespace MahjongRising.code;

// ═══════════════════════════════════════════════════════
// 示例 Mod：添加"诅咒牌"和"净化"动作 + "破咒者"自定义役
//
// 演示 Mod 可扩展的所有维度：
//   1. 自定义牌种 + TileVisual（CurseTile + 外观资源）
//   2. 自定义动作验证器（PurifyValidator）
//   3. 自定义 PlayerAction + Handler（PurifyAction）
//   4. 自定义役种 — 使用新 IYakuRule 接口（一步注册逻辑+番数+显示）
//   5. 自定义动作按钮外观（净化按钮）
// ═══════════════════════════════════════════════════════

public class ExampleCurseMod : IMahjongMod
{
    public string ModId => "example.curse_mod";
    public string DisplayName => "诅咒牌 Mod";
    public string Version => "2.0.0";

    public void Register(ModRegistrationContext ctx)
    {
        string modRoot = ctx.ModResourceRoot; // user://mods/example.curse_mod

        // 1. 自定义牌种 + 外观
        ctx.Tiles.Register("curse_1", () => new CurseTile("curse", 1), 2);
        ctx.Tiles.Register("curse_2", () => new CurseTile("curse", 2), 2);
        ctx.Resources.RegisterTileVisual(TileVisual.CreateForMod("curse_1", ctx.CurrentModId));
        ctx.Resources.RegisterTileVisual(TileVisual.CreateForMod("curse_2", ctx.CurrentModId));

        // 2. 自定义动作验证器
        ctx.Validators.Register("purify", new PurifyValidator());

        // 3. 自定义动作处理器
        ctx.ActionHandlers.Register<PurifyAction>(new PurifyActionHandler());

        // 4. 自定义役种 — 一步注册（逻辑 + 番数 + 显示资源）
        ctx.YakuRules.Register(new CurseBreakerRule(modRoot));

        // 5. 自定义动作按钮
        ctx.Resources.RegisterActionButton(new ActionButtonVisual
        {
            ActionType = "purify",
            DisplayName = "Purify", DisplayNameJp = "浄化", DisplayNameCn = "净化",
            NormalTexturePath = $"{modRoot}/ui/actions/purify_normal.png",
            HoverTexturePath = $"{modRoot}/ui/actions/purify_hover.png",
            PressedTexturePath = $"{modRoot}/ui/actions/purify_pressed.png",
            DisabledTexturePath = $"{modRoot}/ui/actions/purify_disabled.png",
            IconPath = $"{modRoot}/ui/icons/purify.png",
            ExecuteSfxPath = $"{modRoot}/audio/sfx/purify.ogg",
            DisplayOrder = 65,
            ColorTheme = "purple"
        });
    }

    public void Unregister(ModRegistrationContext ctx)
    {
        ctx.Tiles.Unregister("curse_1");
        ctx.Tiles.Unregister("curse_2");
        ctx.Resources.UnregisterTileVisual("curse_1");
        ctx.Resources.UnregisterTileVisual("curse_2");
        ctx.Validators.Unregister("purify", "mod.curse.purify");
        ctx.YakuRules.Unregister("mod.curse_breaker");
        ctx.Resources.UnregisterActionButton("purify");
    }
}

// ── 自定义牌 ──

public class CurseTile : MahjongTile
{
    public override string TileCode => $"curse_{Rank}";
    public override MahjongAction? Action => new CurseTileAction();
    public CurseTile(string category, int rank) : base(category, rank)
    {
        Tags.Add("special"); Tags.Add("curse");
    }
}

public class CurseTileAction : MahjongAction
{
    public override Task OnDraw(MahjongActionContext context)
    {
        context.Owner.RuntimeTags.Add("cursed");
        context.Owner.Counters["curse_stacks"] =
            context.Owner.Counters.GetValueOrDefault("curse_stacks") + 1;
        return Task.CompletedTask;
    }

    public override Task OnDiscard(MahjongActionContext context)
    {
        if (context.Owner.Counters.ContainsKey("curse_stacks"))
        {
            context.Owner.Counters["curse_stacks"]--;
            if (context.Owner.Counters["curse_stacks"] <= 0)
            {
                context.Owner.RuntimeTags.Remove("cursed");
                context.Owner.Counters.Remove("curse_stacks");
            }
        }
        return Task.CompletedTask;
    }
}

// ── 自定义动作验证器 ──

public class PurifyValidator : ITileActionValidator
{
    public string ValidatorId => "mod.curse.purify";

    public ValidationResult Validate(
        MahjongGameState gameState, PlayerState player,
        MahjongTileState targetTile, ValidationContext context)
    {
        if (!targetTile.Tile.Tags.Contains("curse"))
            return ValidationResult.Fail("只能对诅咒牌执行净化");
        if (!player.RuntimeTags.Contains("cursed"))
            return ValidationResult.Fail("没有诅咒状态");

        var numberTiles = player.Hand
            .Where(t => t.Tile.Tags.Contains("number") && !t.IsLocked)
            .GroupBy(t => t.Tile.Rank)
            .Where(g => g.Select(t => t.Tile.Category).Distinct().Count() >= 3)
            .ToList();

        if (numberTiles.Count == 0)
            return ValidationResult.Fail("需要3张相同数字的不同花色牌");

        var options = numberTiles.Select(group =>
        {
            var byCat = group.GroupBy(t => t.Tile.Category).Select(g => g.First()).Take(3).ToList();
            return new ActionOption
            {
                OptionId = $"purify_{group.Key}",
                InvolvedTileIds = byCat.Select(t => t.InstanceId).ToList()
            };
        }).ToList();

        return ValidationResult.Pass(options);
    }
}

// ── 自定义 PlayerAction ──

public class PurifyAction : PlayerAction
{
    public override int BasePriority => 60;
    public string OptionId { get; init; } = "";
    public PurifyAction(int seat) : base(seat) { }
}

public class PurifyActionHandler : IPlayerActionHandler<PurifyAction>
{
    public Task<MahjongRuleResult> Execute(
        MahjongGameState gameState, PurifyAction action, MahjongRuleEngine engine)
    {
        var player = gameState.GetPlayer(action.Seat);
        player.RuntimeTags.Remove("cursed");
        player.Counters.Remove("curse_stacks");

        gameState.EventLog.Add(new GameEvent
        {
            EventType = "purify", Seat = action.Seat,
            Payload = new() { ["optionId"] = action.OptionId }
        });

        return Task.FromResult(MahjongRuleResult.Ok("净化成功！"));
    }
}

// ── 自定义役种（新 IYakuRule 接口） ──

/// <summary>
/// "破咒者"：和牌时如果曾有诅咒状态且已净化，+2 番。
/// 一步注册 = 判定逻辑 + 番数 + 显示资源。
/// </summary>
public class CurseBreakerRule : IYakuRule
{
    public string YakuId => "mod.curse_breaker";

    public YakuDefinition Definition { get; }

    public CurseBreakerRule(string modRoot)
    {
        Definition = new YakuDefinition
        {
            YakuId = "mod.curse_breaker",
            NameJp = "破咒者",
            NameRomaji = "Hajushi",
            NameCn = "破咒者",
            NameEn = "Curse Breaker",
            HanClosed = 2,
            HanOpen = 2,
            Category = "special",
            DescriptionEn = "Win after purifying a curse this round",
            BannerPath = $"{modRoot}/ui/yaku/curse_breaker_banner.png",
            IconPath = $"{modRoot}/ui/yaku/curse_breaker_icon.png",
            AnnouncementSfxPath = $"{modRoot}/audio/sfx/curse_breaker.ogg"
        };
    }

    public YakuResult? Evaluate(YakuEvalContext context)
    {
        bool wasCursed = context.GameState.EventLog
            .Any(e => e.EventType == "purify" && e.Seat == context.Player.Seat);
        bool stillCursed = context.Player.RuntimeTags.Contains("cursed");

        if (!wasCursed || stillCursed) return null;

        return new YakuResult
        {
            YakuId = YakuId,
            Name = Definition.NameJp,
            Han = context.IsMenzen ? Definition.HanClosed : Definition.HanOpen
        };
    }
}
