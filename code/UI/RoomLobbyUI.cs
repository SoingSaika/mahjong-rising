using Godot;
using MahjongRising.code.Session;

namespace MahjongRising.code.UI;

/// <summary>
/// 房间大厅 UI。显示 4 个座位槽，可添加/移除 AI，点击开始。
/// </summary>
public partial class RoomLobbyUI : Control
{
    private Label[] _slotLabels = new Label[4];
    private Button[] _aiButtons = new Button[4];
    private Button _startBtn = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 12);
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 40);
        margin.AddThemeConstantOverride("margin_bottom", 40);
        margin.AddChild(root);
        AddChild(margin);

        var title = new Label { Text = "房间大厅", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        root.AddChild(title);
        root.AddChild(new HSeparator());

        var session = RoomManager.Instance.CurrentSession;

        // 4 个座位
        for (int i = 0; i < 4; i++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            _slotLabels[i] = new Label { CustomMinimumSize = new Vector2(300, 0) };
            row.AddChild(_slotLabels[i]);

            int seat = i;
            _aiButtons[i] = new Button { CustomMinimumSize = new Vector2(120, 40) };
            _aiButtons[i].Pressed += () => ToggleAi(seat);
            row.AddChild(_aiButtons[i]);

            root.AddChild(row);
        }

        root.AddChild(new HSeparator());

        _statusLabel = new Label { Text = "等待开始..." };
        root.AddChild(_statusLabel);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 16);

        _startBtn = new Button { Text = "开始游戏", CustomMinimumSize = new Vector2(200, 50) };
        _startBtn.Pressed += OnStartPressed;
        btnRow.AddChild(_startBtn);

        var backBtn = new Button { Text = "返回", CustomMinimumSize = new Vector2(120, 50) };
        backBtn.Pressed += () => { RoomManager.Instance.LeaveRoom(); GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn"); };
        btnRow.AddChild(backBtn);

        root.AddChild(btnRow);

        UpdateSlots();
    }

    private void ToggleAi(int seat)
    {
        var session = RoomManager.Instance.CurrentSession;
        if (session == null) return;

        // seat 0 是房主/本机玩家，不能改
        if (seat == 0) return;

        if (session.IsAiSeat(seat))
            session.RemoveAi(seat);
        else
            session.SetAiPlayer(seat, RoomManager.Instance.CurrentConfig?.AiDifficulty ?? "normal");

        UpdateSlots();
    }

    private void UpdateSlots()
    {
        var session = RoomManager.Instance.CurrentSession;
        if (session == null) return;

        for (int i = 0; i < 4; i++)
        {
            bool isHuman = !session.IsAiSeat(i) && session.GameState.Players.Count > i && session.GameState.Players[i].PeerId > 0 && session.GameState.Players[i].PeerId < AiPlayerAdapter_PeerBase;
            bool isAi = session.IsAiSeat(i);
            bool isEmpty = !isHuman && !isAi;

            string wind = i switch { 0 => "东", 1 => "南", 2 => "西", _ => "北" };

            if (i == 0)
            {
                _slotLabels[i].Text = $"座位 {i} ({wind}): 你 [房主]";
                _aiButtons[i].Text = "—";
                _aiButtons[i].Disabled = true;
            }
            else if (isAi)
            {
                _slotLabels[i].Text = $"座位 {i} ({wind}): AI ({RoomManager.Instance.CurrentConfig?.AiDifficulty ?? "normal"})";
                _aiButtons[i].Text = "移除 AI";
                _aiButtons[i].Disabled = false;
            }
            else if (isHuman)
            {
                _slotLabels[i].Text = $"座位 {i} ({wind}): 玩家 (已连接)";
                _aiButtons[i].Text = "—";
                _aiButtons[i].Disabled = true;
            }
            else
            {
                _slotLabels[i].Text = $"座位 {i} ({wind}): 空位";
                _aiButtons[i].Text = "添加 AI";
                _aiButtons[i].Disabled = false;
            }
        }
    }

    private void OnStartPressed()
    {
        var session = RoomManager.Instance.CurrentSession;
        if (session == null) return;

        // 空位自动补 AI
        for (int i = 0; i < 4; i++)
        {
            if (!session.IsAiSeat(i) && (session.GameState.Players.Count <= i || session.GameState.Players[i].PeerId == 0))
                session.SetAiPlayer(i, RoomManager.Instance.CurrentConfig?.AiDifficulty ?? "normal");
        }

        _statusLabel.Text = "正在启动...";
        _startBtn.Disabled = true;

        // 切换到游戏场景（游戏在 GameBoardUI._Ready 中启动）
        GetTree().ChangeSceneToFile("res://scenes/game_board.tscn");
    }

    // AiPlayerAdapter.AiPeerBase 的值
    private const long AiPlayerAdapter_PeerBase = 100000;
}
