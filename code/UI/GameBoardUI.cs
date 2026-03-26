using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.Game.Rpc;
using MahjongRising.code.Session;

namespace MahjongRising.code.UI;

/// <summary>
/// 完整的对局界面。
/// 订阅 RPC 事件 → 更新本地状态 → 刷新 UI。
/// 玩家点击手牌弃牌，点击动作按钮响应。
/// </summary>
public partial class GameBoardUI : Control
{
    // ── 客户端状态 ──
    private int _mySeat = -1;
    private List<TileDto> _myHand = new();
    private PlayerInfoDto[] _players = new PlayerInfoDto[4];
    private string _phase = "";
    private int _currentSeat;
    private int _wallRemaining;
    private List<TileDto> _doraIndicators = new();
    private AvailableActionsDto? _pendingActions;
    private bool _isSelfActionPhase;
    private string _roundInfo = "";

    // ── UI 节点 ──
    private Label _infoLabel = null!;
    private Label _statusLabel = null!;
    private HBoxContainer _handContainer = null!;
    private VBoxContainer _playersContainer = null!;
    private HBoxContainer _actionContainer = null!;
    private Label _resultLabel = null!;
    private PlayerActionRpcManager? _rpc;

    public override void _Ready()
    {
        BuildUI();

        // 延迟订阅（等 GameSession 创建 RPC 后）
        CallDeferred(nameof(SubscribeRpc));
    }

    private void SubscribeRpc()
    {
        var session = RoomManager.Instance?.CurrentSession;
        if (session == null) { _statusLabel.Text = "等待服务器..."; return; }

        _rpc = session.Rpc;
        _rpc.OnGameInit += HandleGameInit;
        _rpc.OnDraw += HandleDraw;
        _rpc.OnDiscard += HandleDiscard;
        _rpc.OnRiichi += HandleRiichi;
        _rpc.OnReactionWindow += HandleReactionWindow;
        _rpc.OnReactionResult += HandleReactionResult;
        _rpc.OnDoraReveal += HandleDoraReveal;
        _rpc.OnTurnStart += HandleTurnStart;
        _rpc.OnPhaseChange += HandlePhaseChange;
        _rpc.OnSelfActions += HandleSelfActions;
        _rpc.OnRoundEnd += HandleRoundEnd;
        _rpc.OnError += HandleError;

        _statusLabel.Text = "已连接，等待开局...";

        // 服务端（房主或单人）：订阅完成后开始游戏
        if (Multiplayer.IsServer() && session.GameState.Phase == Game.Rules.TurnPhase.RoundStart)
        {
            RoomManager.Instance.StartGame();
        }
    }

    // ═══════════════════════════════
    // UI 构建
    // ═══════════════════════════════

    private void BuildUI()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 16);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        // 1. 信息栏
        _infoLabel = new Label { Text = "等待初始化..." };
        _infoLabel.AddThemeFontSizeOverride("font_size", 18);
        root.AddChild(_infoLabel);

        root.AddChild(new HSeparator());

        // 2. 其他玩家区域
        _playersContainer = new VBoxContainer();
        _playersContainer.AddThemeConstantOverride("separation", 4);
        for (int i = 0; i < 4; i++)
        {
            var lbl = new Label { Name = $"Player{i}" };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            _playersContainer.AddChild(lbl);
        }
        root.AddChild(_playersContainer);

        root.AddChild(new HSeparator());

        // 3. 结果显示（和牌/流局时显示）
        _resultLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
        _resultLabel.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(_resultLabel);

        // 4. 状态栏
        _statusLabel = new Label { Text = "加载中...", HorizontalAlignment = HorizontalAlignment.Center };
        _statusLabel.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(_statusLabel);

        // 5. 动作按钮栏
        _actionContainer = new HBoxContainer { Visible = false };
        _actionContainer.AddThemeConstantOverride("separation", 8);
        root.AddChild(_actionContainer);

        root.AddChild(new HSeparator());

        // 6. 我的手牌
        var handLabel = new Label { Text = "我的手牌：" };
        handLabel.AddThemeFontSizeOverride("font_size", 16);
        root.AddChild(handLabel);

        var handScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 80) };
        _handContainer = new HBoxContainer();
        _handContainer.AddThemeConstantOverride("separation", 4);
        handScroll.AddChild(_handContainer);
        root.AddChild(handScroll);

        // 7. 退出按钮
        var exitBtn = new Button { Text = "退出对局", CustomMinimumSize = new Vector2(120, 36) };
        exitBtn.Pressed += () => { RoomManager.Instance.LeaveRoom(); GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn"); };
        root.AddChild(exitBtn);
    }

    // ═══════════════════════════════
    // RPC 事件处理
    // ═══════════════════════════════

    private void HandleGameInit(GameInitEventDto e)
    {
        _mySeat = e.MySeat;
        _doraIndicators = e.DoraIndicators;
        _wallRemaining = e.WallRemaining;
        _roundInfo = $"{WindStr(e.RoundWind)}风 | 庄:{WindStr(e.DealerSeat)} | 本场{e.Honba}";

        for (int i = 0; i < e.Players.Count && i < 4; i++) _players[i] = e.Players[i];

        // 自己的手牌
        var myInfo = e.Players.FirstOrDefault(p => p.Seat == _mySeat);
        _myHand = myInfo?.HandTiles ?? new();

        RefreshAll();
        _statusLabel.Text = "对局开始！";
        _resultLabel.Visible = false;
    }

    private void HandleDraw(DrawEventDto e)
    {
        _wallRemaining = e.WallRemaining;
        if (e.Seat == _mySeat && e.Tile != null && e.Tile.TileCode != "hidden")
        {
            _myHand.Add(e.Tile);
            RefreshHand();
        }
        if (_players[e.Seat] != null) _players[e.Seat]!.HandCount++;
        RefreshPlayers();
        RefreshInfo();
    }

    private void HandleDiscard(DiscardEventDto e)
    {
        // 如果是自己弃的牌，从手牌移除
        if (e.Seat == _mySeat)
        {
            _myHand.RemoveAll(t => t.InstanceId == e.Tile.InstanceId);
            RefreshHand();
        }
        if (_players[e.Seat] != null)
        {
            _players[e.Seat]!.HandCount--;
            _players[e.Seat]!.Discards.Add(e.Tile);
        }
        RefreshPlayers();

        string name = e.Seat == _mySeat ? "你" : $"P{e.Seat}";
        string riichi = e.IsRiichi ? " [立直!]" : "";
        _statusLabel.Text = $"{name} 弃 {TileName(e.Tile)}{riichi}";
    }

    private void HandleRiichi(RiichiEventDto e)
    {
        if (_players[e.Seat] != null) { _players[e.Seat]!.IsRiichi = true; _players[e.Seat]!.Score = e.NewScore; }
        RefreshPlayers();
    }

    private void HandleReactionWindow(ReactionWindowEventDto e)
    {
        _pendingActions = e.AvailableActions;
        _isSelfActionPhase = false;
        ShowActionButtons(e.AvailableActions, $"P{e.SourceSeat} 弃了 {TileName(e.SourceTile)} — 你要？");
    }

    private void HandleSelfActions(SelfActionsEventDto e)
    {
        _pendingActions = e.AvailableActions;
        _isSelfActionPhase = true;
        ShowActionButtons(e.AvailableActions, "自摸动作可用！（或直接点手牌弃牌）");
    }

    private void HandleReactionResult(ReactionResultEventDto e)
    {
        HideActionButtons();
        string name = e.WinnerSeat == _mySeat ? "你" : $"P{e.WinnerSeat}";
        string actName = e.ActionType switch { "chi" => "吃", "peng" => "碰", "gang" => "杠", _ => e.ActionType };
        _statusLabel.Text = $"{name} {actName}！";

        // 更新副露信息
        if (_players[e.WinnerSeat] != null && e.NewMeld != null)
            _players[e.WinnerSeat]!.Melds.Add(e.NewMeld);
        RefreshPlayers();
    }

    private void HandleDoraReveal(DoraRevealEventDto e)
    {
        _doraIndicators.Add(e.NewIndicator);
        RefreshInfo();
    }

    private void HandleTurnStart(TurnStartEventDto e)
    {
        _currentSeat = e.Seat;
        HideActionButtons();
        _pendingActions = null;
        _isSelfActionPhase = false;
        if (e.Seat == _mySeat)
            _statusLabel.Text = "你的回合";
        else
            _statusLabel.Text = $"P{e.Seat} 的回合";
    }

    private void HandlePhaseChange(PhaseChangeEventDto e)
    {
        _phase = e.Phase;
        _currentSeat = e.CurrentSeat;
    }

    private void HandleRoundEnd(RoundEndEventDto e)
    {
        HideActionButtons();
        _resultLabel.Visible = true;

        if (e.Reason == "draw")
        {
            _resultLabel.Text = "流局";
            _statusLabel.Text = "牌山已尽，流局";
        }
        else
        {
            string winner = e.WinnerSeat == _mySeat ? "你" : $"P{e.WinnerSeat}";
            string type = e.Reason == "tsumo" ? "自摸" : "荣和";
            string yakuStr = string.Join(", ", e.YakuIds ?? new());
            _resultLabel.Text = $"{winner} {type}！ {e.TotalHan}番{e.Fu}符";
            _statusLabel.Text = $"役: {yakuStr}";
        }

        if (e.ScoreChanges != null)
        {
            foreach (var (seat, change) in e.ScoreChanges)
                if (_players[seat] != null) _players[seat]!.Score += change;
            RefreshPlayers();
        }
    }

    private void HandleError(string msg)
    {
        GD.PrintErr($"[GameBoard] 服务器错误: {msg}");
    }

    // ═══════════════════════════════
    // 玩家操作
    // ═══════════════════════════════

    /// <summary>点击手牌 → 弃牌。</summary>
    private void OnTileClicked(string tileInstanceId)
    {
        if (_rpc == null) return;

        bool canDiscard = _phase is "DiscardPhase" or "SelfActionPhase";
        if (!canDiscard || _currentSeat != _mySeat) return;

        // 发给服务端（peerId=1），不广播
        _rpc.RpcId(1, nameof(PlayerActionRpcManager.RequestDiscard),
            RpcSerializer.Serialize(new DiscardRequestDto { TileInstanceId = tileInstanceId, IsRiichi = false }));

        HideActionButtons();
        _statusLabel.Text = "已弃牌，等待...";
    }

    /// <summary>点击动作按钮。</summary>
    private void OnActionClicked(string actionType, string optionId)
    {
        if (_rpc == null) return;

        if (_isSelfActionPhase)
        {
            _rpc.RpcId(1, nameof(PlayerActionRpcManager.RequestSelfAction),
                RpcSerializer.Serialize(new SelfActionRequestDto { ActionType = actionType, OptionId = optionId }));
        }
        else
        {
            _rpc.RpcId(1, nameof(PlayerActionRpcManager.RequestReaction),
                RpcSerializer.Serialize(new ReactionRequestDto { ActionType = actionType, OptionId = optionId }));
        }

        HideActionButtons();
        _statusLabel.Text = $"已选择: {actionType}";
    }

    // ═══════════════════════════════
    // UI 刷新
    // ═══════════════════════════════

    private void RefreshAll() { RefreshInfo(); RefreshPlayers(); RefreshHand(); }

    private void RefreshInfo()
    {
        string dora = string.Join(" ", _doraIndicators.Select(d => TileName(d)));
        _infoLabel.Text = $"{_roundInfo} | 残{_wallRemaining}张 | 宝牌: {dora}";
    }

    private void RefreshPlayers()
    {
        for (int i = 0; i < 4; i++)
        {
            var lbl = _playersContainer.GetChild<Label>(i);
            var p = _players[i];
            if (p == null) { lbl.Text = $"P{i}: (空)"; continue; }

            string wind = WindStr(i);
            string me = i == _mySeat ? " [你]" : "";
            string riichi = p.IsRiichi ? " [立直]" : "";
            string melds = p.Melds.Count > 0 ? " 副露:" + string.Join(" ", p.Melds.Select(MeldStr)) : "";
            string discards = p.Discards.Count > 0 ? " 弃:" + string.Join(" ", p.Discards.TakeLast(8).Select(TileName)) : "";

            lbl.Text = $"P{i}{me} ({wind}) 分:{p.Score} 手:{p.HandCount}{riichi}{melds}{discards}";
        }
    }

    private void RefreshHand()
    {
        // 清除旧按钮
        foreach (var child in _handContainer.GetChildren()) child.QueueFree();

        // 按 TileCode 排序
        var sorted = _myHand.OrderBy(t => t.TileCode).ToList();

        foreach (var tile in sorted)
        {
            var btn = new Button
            {
                Text = TileName(tile),
                CustomMinimumSize = new Vector2(56, 72),
                TooltipText = tile.TileCode
            };
            btn.AddThemeFontSizeOverride("font_size", 18);
            string id = tile.InstanceId;
            btn.Pressed += () => OnTileClicked(id);
            _handContainer.AddChild(btn);
        }
    }

    private void ShowActionButtons(AvailableActionsDto actions, string statusText)
    {
        _statusLabel.Text = statusText;
        _actionContainer.Visible = true;

        foreach (var child in _actionContainer.GetChildren()) child.QueueFree();

        void AddAction(string type, string label, List<ActionOptionDto>? opts)
        {
            if (opts == null || opts.Count == 0) return;
            string optId = opts[0].OptionId;
            var btn = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(80, 50)
            };
            btn.AddThemeFontSizeOverride("font_size", 20);
            btn.Pressed += () => OnActionClicked(type, optId);
            _actionContainer.AddChild(btn);
        }

        AddAction("hu", "和！", actions.Hu);
        AddAction("gang", "杠", actions.Gang);
        AddAction("peng", "碰", actions.Peng);
        AddAction("chi", "吃", actions.Chi);

        // Mod 自定义动作
        if (actions.Custom != null)
            foreach (var (key, opts) in actions.Custom)
                AddAction(key, key, opts);

        // 跳过按钮
        if (!_isSelfActionPhase) // 自摸阶段不需要"跳过"（直接弃牌就行）
        {
            var passBtn = new Button { Text = "跳过", CustomMinimumSize = new Vector2(80, 50) };
            passBtn.AddThemeFontSizeOverride("font_size", 20);
            passBtn.Pressed += () => OnActionClicked("pass", "");
            _actionContainer.AddChild(passBtn);
        }
    }

    private void HideActionButtons()
    {
        _actionContainer.Visible = false;
        foreach (var child in _actionContainer.GetChildren()) child.QueueFree();
    }

    // ═══════════════════════════════
    // 牌面文字显示
    // ═══════════════════════════════

    private static string TileName(TileDto tile) => TileCodeToDisplay(tile.TileCode);

    private static string TileCodeToDisplay(string code)
    {
        if (code == "hidden") return "🀫";
        var parts = code.Split('_');
        if (parts.Length < 2) return code;

        string suit = parts[0]; string rank = parts[1];
        bool isRed = parts.Length > 2 && parts[2] == "red";
        string redMark = isRed ? "赤" : "";

        return suit switch
        {
            "man" => $"{redMark}{rank}万",
            "pin" => $"{redMark}{rank}筒",
            "sou" => $"{redMark}{rank}索",
            "wind" => rank switch { "1" => "東", "2" => "南", "3" => "西", "4" => "北", _ => code },
            "dragon" => rank switch { "1" => "白", "2" => "發", "3" => "中", _ => code },
            _ => code // Mod 自定义牌
        };
    }

    private static string MeldStr(MeldDto m)
    {
        string tiles = string.Join("", m.Tiles.Select(TileName));
        string kind = m.Kind switch { "chi" => "吃", "peng" => "碰", "minkan" => "明杠", "ankan" => "暗杠", "kakan" => "加杠", _ => m.Kind };
        return $"[{kind}{tiles}]";
    }

    private static string WindStr(int i) => i switch { 0 => "東", 1 => "南", 2 => "西", 3 => "北", _ => "?" };

    // ═══════════════════════════════
    // 清理
    // ═══════════════════════════════

    public override void _ExitTree()
    {
        if (_rpc != null)
        {
            _rpc.OnGameInit -= HandleGameInit;
            _rpc.OnDraw -= HandleDraw;
            _rpc.OnDiscard -= HandleDiscard;
            _rpc.OnRiichi -= HandleRiichi;
            _rpc.OnReactionWindow -= HandleReactionWindow;
            _rpc.OnReactionResult -= HandleReactionResult;
            _rpc.OnDoraReveal -= HandleDoraReveal;
            _rpc.OnTurnStart -= HandleTurnStart;
            _rpc.OnPhaseChange -= HandlePhaseChange;
            _rpc.OnSelfActions -= HandleSelfActions;
            _rpc.OnRoundEnd -= HandleRoundEnd;
            _rpc.OnError -= HandleError;
        }
    }
}
