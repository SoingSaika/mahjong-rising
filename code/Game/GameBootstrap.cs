using Godot;
using MahjongRising.code.Character;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Game.Rules.Validators.Builtin;
using MahjongRising.code.Mod;
using MahjongRising.code.Player.Actions;
using MahjongRising.code.Resources;
using MahjongRising.code.Yaku;
using MahjongRising.code.Yaku.Builtin;

namespace MahjongRising.code.Game;

/// <summary>
/// 游戏启动引导器（AutoLoad）。
/// 只负责初始化所有注册中心和加载 Mod。
/// 不负责开房间、开局、RPC — 那些由 GameSession / RoomManager 管理。
/// </summary>
public partial class GameBootstrap : Node
{
    // ── 注册中心（全局单例，生命周期 = 整个程序） ──
    public ValidatorRegistry Validators { get; private set; } = null!;
    public PlayerActionHandlerRegistry ActionHandlers { get; private set; } = null!;
    public TileRegistry Tiles { get; private set; } = null!;
    public YakuDefinitionRegistry YakuDefinitions { get; private set; } = null!;
    public YakuRuleRegistry YakuRules { get; private set; } = null!;
    public ResourceRegistry Resources { get; private set; } = null!;
    public CharacterRegistry Characters { get; private set; } = null!;
    public ScoreCalculator Scoring { get; private set; } = null!;

    private ModLoader _modLoader = null!;

    public override void _Ready()
    {
        InitRegistries();
        RegisterBuiltinValidators();
        RegisterBuiltinYaku();
        RegisterBuiltinTiles();
        RegisterBuiltinResources();
        RegisterBuiltinCharacters();
        LoadMods();
        GD.Print("[Bootstrap] 初始化完成，等待创建房间");
    }

    private void InitRegistries()
    {
        Validators = new ValidatorRegistry();
        ActionHandlers = new PlayerActionHandlerRegistry();
        Tiles = new TileRegistry();
        YakuDefinitions = new YakuDefinitionRegistry();
        YakuRules = new YakuRuleRegistry();
        Resources = new ResourceRegistry();
        Characters = new CharacterRegistry();
        Scoring = new ScoreCalculator();
    }

    private void RegisterBuiltinValidators()
    {
        Validators.Register("chi", new ChiValidator());
        Validators.Register("peng", new PengValidator());
        Validators.Register("gang", new GangValidator());
    }

    private void RegisterBuiltinYaku()
    {
        YakuDefinitions.RegisterAllBuiltinDefinitions();
        Defs.Init(YakuDefinitions);

        YakuRules.Register(new RiichiRule());
        YakuRules.Register(new DoubleRiichiRule());
        YakuRules.Register(new IppatsuRule());
        YakuRules.Register(new MenzenTsumoRule());
        YakuRules.Register(new TanyaoRule());
        YakuRules.Register(new PinfuRule());
        YakuRules.Register(new IipeikoRule());
        YakuRules.Register(new YakuhaiHakuRule());
        YakuRules.Register(new YakuhaiHatsuRule());
        YakuRules.Register(new YakuhaiChunRule());
        YakuRules.Register(new YakuhaiBakazeRule());
        YakuRules.Register(new YakuhaiJikazeRule());
        YakuRules.Register(new RinshanRule());
        YakuRules.Register(new ChankanRule());
        YakuRules.Register(new HaiteiRule());
        YakuRules.Register(new HouteiRule());
        YakuRules.Register(new ChiitoitsuRule());
        YakuRules.Register(new ToitoiRule());
        YakuRules.Register(new SanAnkouRule());
        YakuRules.Register(new SanShokuDoujunRule());
        YakuRules.Register(new SanShokuDoukouRule());
        YakuRules.Register(new IttsuRule());
        YakuRules.Register(new ChantaRule());
        YakuRules.Register(new SanKantsuRule());
        YakuRules.Register(new HonroutouRule());
        YakuRules.Register(new ShouSangenRule());
        YakuRules.Register(new RyanpeikoRule());
        YakuRules.Register(new JunchanRule());
        YakuRules.Register(new HonitsuRule());
        YakuRules.Register(new ChinitsuRule());
        YakuRules.Register(new KokushiRule());
        YakuRules.Register(new SuuAnkouRule());
        YakuRules.Register(new DaiSangenRule());
        YakuRules.Register(new ShouSuushiiRule());
        YakuRules.Register(new DaiSuushiiRule());
        YakuRules.Register(new TsuuiisouRule());
        YakuRules.Register(new ChinroutouRule());
        YakuRules.Register(new RyuuiisouRule());
        YakuRules.Register(new ChuurenRule());
        YakuRules.Register(new SuuKantsuRule());
        YakuRules.Register(new TenhouRule());
        YakuRules.Register(new ChiihouRule());
        YakuRules.Register(new DoraRule());
        YakuRules.Register(new AkadoraRule());

        Validators.Register("hu", new HuValidator(YakuRules));
    }

    private void RegisterBuiltinTiles()
    {
        foreach (var s in new[] { "man", "pin", "sou" })
        {
            string suit = s;
            for (int r = 1; r <= 9; r++) { int rank = r; string code = $"{suit}_{rank}"; Tiles.Register(code, () => new StandardNumberTile(suit, rank), 4); Resources.RegisterTileVisual(TileVisual.CreateDefault(code)); }
            string red = $"{suit}_5_red"; Tiles.Register(red, () => new StandardNumberTile(suit, 5) { Variants = { "red" } }, 0);
            var rv = TileVisual.CreateDefault(red); rv.HighlightShaderPath = $"{ResourcePathConstants.VfxShaders}/red_dora_glow.gdshader"; Resources.RegisterTileVisual(rv);
        }
        for (int r = 1; r <= 4; r++) { int rank = r; string code = $"wind_{rank}"; Tiles.Register(code, () => new StandardHonorTile("wind", rank), 4); Resources.RegisterTileVisual(TileVisual.CreateDefault(code)); }
        for (int r = 1; r <= 3; r++) { int rank = r; string code = $"dragon_{rank}"; Tiles.Register(code, () => new StandardHonorTile("dragon", rank), 4); Resources.RegisterTileVisual(TileVisual.CreateDefault(code)); }
    }

    private void RegisterBuiltinResources()
    {
        foreach (var a in new[] { "chi", "peng", "gang", "hu", "riichi", "pass", "tsumo", "ron", "ankan", "kakan" })
            Resources.RegisterActionButton(ActionButtonVisual.CreateBuiltin(a));
        Resources.RegisterTableTheme(TableTheme.CreateDefault());
    }

    private void RegisterBuiltinCharacters()
    {
        var akitsuki = CharacterDefinition.CreateBuiltin("akitsuki", "Akitsuki", "明月", "明月");
        akitsuki.Title = "月下の雀士"; akitsuki.Rarity = "common";
        akitsuki.VoicePack.PopulateFromDirectory($"{ResourcePathConstants.CharacterVoice}/akitsuki");
        Characters.RegisterCharacter(akitsuki);
        // 其余角色同理，此处精简示例
    }

    private void LoadMods()
    {
        _modLoader = new ModLoader();
        _modLoader.LoadAll(new ModRegistrationContext
        {
            Validators = Validators, ActionHandlers = ActionHandlers, Tiles = Tiles,
            YakuRules = YakuRules, YakuDefinitions = YakuDefinitions,
            Resources = Resources, Characters = Characters
        });
    }
}

// ── 内置牌类型 ──

public class StandardNumberTile : MahjongRising.code.Mahjong.Tiles.MahjongTile
{
    public override string TileCode => Variants.Contains("red") ? $"{Category}_{Rank}_red" : $"{Category}_{Rank}";
    public StandardNumberTile(string category, int rank) : base(category, rank) { Tags.Add("number"); if (rank is 1 or 9) Tags.Add("terminal"); }
}

public class StandardHonorTile : MahjongRising.code.Mahjong.Tiles.MahjongTile
{
    public override string TileCode => $"{Category}_{Rank}";
    public StandardHonorTile(string category, int rank) : base(category, rank) { Tags.Add("honor"); if (category is "wind" or "风") Tags.Add("wind"); if (category is "dragon" or "三元") Tags.Add("dragon"); }
}