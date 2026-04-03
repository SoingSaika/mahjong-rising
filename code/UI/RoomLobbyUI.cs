using System.Linq;
using Godot;
using MahjongRising.code.Session;
using MahjongRising.code.Session.Rpc;
using RoomRpcManager = MahjongRising.code.Session.Rpc.RoomRpcManager;

namespace MahjongRising.code.UI;

/// <summary>
/// 房间大厅 UI。
/// 房主和客户端都订阅 RoomRpcManager 事件，状态完全同步。
/// </summary>
public partial class RoomLobbyUI : Control
{
    private Label[] _slotLabels = new Label[4];
    private Button[] _slotButtons = new Button[4];
    private Button _startBtn = null!;
    private Label _statusLabel = null!;
    private bool _isHost;
    private RoomRpcManager? _roomRpc;

    public override void _Ready()
    {
        _isHost = RoomManager.Instance.IsHost;

        BuildUI();
        SubscribeEvents();
    }

    private void BuildUI()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(s, 40);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var title = new Label
        {
            Text = _isHost ? "房间大厅 (房主)" : "房间大厅 (客户端)",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        root.AddChild(title);
        root.AddChild(new HSeparator());

        string[] winds = { "东", "南", "西", "北" };
        for (int i = 0; i < 4; i++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            _slotLabels[i] = new Label
            {
                Text = $"座位 {i} ({winds[i]}): 等待...",
                CustomMinimumSize = new Vector2(300, 0)
            };
            row.AddChild(_slotLabels[i]);

            int seat = i;
            _slotButtons[i] = new Button
            {
                Text = "添加AI",
                CustomMinimumSize = new Vector2(120, 40),
                Visible = _isHost && i > 0
            };
            _slotButtons[i].Pressed += () => OnSlotButtonPressed(seat);
            row.AddChild(_slotButtons[i]);

            root.AddChild(row);
        }

        root.AddChild(new HSeparator());

        _statusLabel = new Label { Text = _isHost ? "等待玩家加入或添加 AI..." : "等待房主操作..." };
        root.AddChild(_statusLabel);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 16);

        _startBtn = new Button
        {
            Text = "开始游戏",
            CustomMinimumSize = new Vector2(200, 50),
            Visible = _isHost
        };
        _startBtn.Pressed += OnStartPressed;
        btnRow.AddChild(_startBtn);

        var backBtn = new Button { Text = "返回", CustomMinimumSize = new Vector2(120, 50) };
        backBtn.Pressed += () =>
        {
            RoomManager.Instance.LeaveRoom();
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
        };
        btnRow.AddChild(backBtn);
        root.AddChild(btnRow);
    }

    private void SubscribeEvents()
    {
        var session = RoomManager.Instance.CurrentSession;
        if (session == null) { _statusLabel.Text = "错误：无会话"; return; }

        _roomRpc = session.RoomRpc;

        // 核心：订阅房间状态同步
        _roomRpc.OnRoomStateUpdated += OnRoomStateUpdated;

        // 订阅游戏开始通知（房主和客户端都收到）
        _roomRpc.OnGameStarting += OnGameStarting;

        // 客户端收到座位分配
        _roomRpc.OnSeatAssigned += OnSeatAssigned;

        // 错误
        _roomRpc.OnRoomError += msg => { _statusLabel.Text = $"错误: {msg}"; };

        // 立即加载当前房间状态（ServerSetHost 的广播在 UI 订阅前已发出，这里补上）
        var currentState = _roomRpc.GetCurrentState();
        OnRoomStateUpdated(currentState);
    }

    // ═══ 事件处理 ═══

    private void OnRoomStateUpdated(RoomStateDto state)
    {
        string[] winds = { "东", "南", "西", "北" };

        for (int i = 0; i < 4 && i < state.Slots.Count; i++)
        {
            var slot = state.Slots[i];
            string w = i < winds.Length ? winds[i] : "?";
            bool isMe = i == RoomManager.Instance.MySeat;
            string meTag = isMe ? " [你]" : "";

            _slotLabels[i].Text = slot.Status switch
            {
                "Human" => $"座位 {i} ({w}): {slot.DisplayName}{meTag}",
                "Ai" => $"座位 {i} ({w}): {slot.DisplayName}",
                _ => $"座位 {i} ({w}): 空位"
            };

            // 按钮逻辑（仅房主）
            if (_isHost && i > 0)
            {
                _slotButtons[i].Visible = true;
                if (slot.Status == "Ai")
                {
                    _slotButtons[i].Text = "移除AI";
                    _slotButtons[i].Disabled = false;
                }
                else if (slot.Status == "Human")
                {
                    _slotButtons[i].Text = "已有玩家";
                    _slotButtons[i].Disabled = true;
                }
                else
                {
                    _slotButtons[i].Text = "添加AI";
                    _slotButtons[i].Disabled = false;
                }
            }
        }

        // 更新状态提示
        int humanCount = state.Slots.Count(s => s.Status == "Human");
        int aiCount = state.Slots.Count(s => s.Status == "Ai");
        int emptyCount = state.Slots.Count(s => s.Status == "Empty");
        _statusLabel.Text = $"玩家: {humanCount} | AI: {aiCount} | 空位: {emptyCount}";
    }

    private void OnSeatAssigned(int seat)
    {
        RoomManager.Instance.MySeat = seat;
        _statusLabel.Text = $"已分配座位: {seat}";
    }

    private void OnGameStarting()
    {
        _statusLabel.Text = "游戏启动中...";
        _startBtn.Disabled = true;

        // 所有人（房主 + 客户端）切换到游戏场景
        GetTree().ChangeSceneToFile("res://scenes/GameBoard.tscn");
    }

    // ═══ 房主操作 ═══

    private void OnSlotButtonPressed(int seat)
    {
        if (!_isHost || _roomRpc == null) return;

        var session = RoomManager.Instance.CurrentSession;
        if (session == null) return;

        // 读取当前状态判断切换
        var status = session.RoomRpc.GetStatus(seat);
        if (status == SlotStatus.Ai)
            RoomManager.Instance.RemoveSlot(seat);
        else if (status == SlotStatus.Empty)
            RoomManager.Instance.AddAi(seat);
        // Human 状态按钮已 disabled
    }

    private void OnStartPressed()
    {
        if (!_isHost) return;
        RoomManager.Instance.StartGame();
    }

    // ═══ 清理 ═══

    public override void _ExitTree()
    {
        if (_roomRpc != null)
        {
            _roomRpc.OnRoomStateUpdated -= OnRoomStateUpdated;
            _roomRpc.OnGameStarting -= OnGameStarting;
            _roomRpc.OnSeatAssigned -= OnSeatAssigned;
        }
    }
}