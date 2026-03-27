using Godot;
using MahjongRising.code.Game.Rpc;
using MahjongRising.code.Session;

namespace MahjongRising.code.UI;

public partial class RoomLobbyUI : Control
{
	private Label[] _slotLabels = new Label[4];
	private Button[] _aiButtons = new Button[4];
	private Button _startBtn = null!;
	private Label _statusLabel = null!;
	private bool _isHost;

	public override void _Ready()
	{
		_isHost = RoomManager.Instance.IsHost;

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		foreach (var s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
			margin.AddThemeConstantOverride(s, 40);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		var title = new Label { Text = _isHost ? "房间大厅 (房主)" : "房间大厅 (等待中)", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 32);
		root.AddChild(title);
		root.AddChild(new HSeparator());

		for (int i = 0; i < 4; i++)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			_slotLabels[i] = new Label { CustomMinimumSize = new Vector2(300, 0) };
			row.AddChild(_slotLabels[i]);
			int seat = i;
			_aiButtons[i] = new Button { CustomMinimumSize = new Vector2(120, 40) };
			_aiButtons[i].Pressed += () => ToggleAi(seat);
			_aiButtons[i].Visible = _isHost;
			row.AddChild(_aiButtons[i]);
			root.AddChild(row);
		}

		root.AddChild(new HSeparator());
		_statusLabel = new Label { Text = _isHost ? "等待开始..." : "等待房主开始游戏..." };
		root.AddChild(_statusLabel);

		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 16);

		_startBtn = new Button { Text = "开始游戏", CustomMinimumSize = new Vector2(200, 50), Visible = _isHost };
		_startBtn.Pressed += OnStartPressed;
		btnRow.AddChild(_startBtn);

		var backBtn = new Button { Text = "返回", CustomMinimumSize = new Vector2(120, 50) };
		backBtn.Pressed += () => { RoomManager.Instance.LeaveRoom(); GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn"); };
		btnRow.AddChild(backBtn);
		root.AddChild(btnRow);

		// 客户端：监听 GameInit 事件，收到说明游戏开始了
		if (!_isHost)
		{
			var session = RoomManager.Instance.CurrentSession;
			if (session?.Rpc != null)
				session.Rpc.OnGameInit += OnClientReceiveGameInit;
		}

		// 监听 peer 变化（房主端）
		if (_isHost)
			NetworkManager.Instance.PeerConnected += OnPeerJoined;

		UpdateSlots();
	}

	private void OnPeerJoined(long peerId)
	{
		// RoomManager.OnPeerConnected 已经分配了座位，只需刷新 UI
		CallDeferred(nameof(UpdateSlots));
	}

	private void OnClientReceiveGameInit(GameInitEventDto e)
	{
		// 客户端收到 GameInit = 游戏开始了
		RoomManager.Instance.MySeat = e.MySeat;

		// 缓存初始数据（GameBoardUI 启动时读取）
		RoomManager.Instance.CurrentSession!.CachedInitData = e;

		// 取消订阅
		var session = RoomManager.Instance.CurrentSession;
		if (session?.Rpc != null)
			session.Rpc.OnGameInit -= OnClientReceiveGameInit;

		// 切换到游戏场景
		GetTree().ChangeSceneToFile("res://scenes/GameBoard.tscn");
	}

	private void ToggleAi(int seat)
	{
		if (!_isHost || seat == 0) return;
		var session = RoomManager.Instance.CurrentSession;
		if (session == null) return;
		if (session.IsAiSeat(seat)) session.RemoveAi(seat);
		else session.SetAiPlayer(seat, RoomManager.Instance.CurrentConfig?.AiDifficulty ?? "normal");
		UpdateSlots();
	}

	private void UpdateSlots()
	{
		var session = RoomManager.Instance.CurrentSession;
		if (session == null) return;
		string[] winds = { "东", "南", "西", "北" };

		for (int i = 0; i < 4; i++)
		{
			bool hasPlayer = session.GameState.Players.Count > i;
			bool isAi = session.IsAiSeat(i);
			bool isHuman = hasPlayer && !isAi && session.GameState.Players[i].PeerId > 0 && session.GameState.Players[i].PeerId < 100000;

			string w = i < winds.Length ? winds[i] : "?";
			if (i == 0 && _isHost) _slotLabels[i].Text = $"座位 {i} ({w}): 你 [房主]";
			else if (isAi) _slotLabels[i].Text = $"座位 {i} ({w}): AI";
			else if (isHuman) _slotLabels[i].Text = $"座位 {i} ({w}): 玩家 (已连接)";
			else _slotLabels[i].Text = $"座位 {i} ({w}): 空位";

			if (_isHost && i > 0)
			{
				_aiButtons[i].Text = isAi ? "移除AI" : "添加AI";
				_aiButtons[i].Disabled = isHuman;
			}
		}
	}

	private void OnStartPressed()
	{
		var session = RoomManager.Instance.CurrentSession;
		if (session == null) return;

		// 空位补 AI
		for (int i = 0; i < 4; i++)
			if (!session.IsAiSeat(i) && (session.GameState.Players.Count <= i || session.GameState.Players[i].PeerId == 0))
				session.SetAiPlayer(i, RoomManager.Instance.CurrentConfig?.AiDifficulty ?? "normal");

		_startBtn.Disabled = true;
		_statusLabel.Text = "正在启动...";

		// 切换到游戏场景（GameBoardUI._Ready 中调 StartGame）
		GetTree().ChangeSceneToFile("res://scenes/GameBoard.tscn");
	}

	public override void _ExitTree()
	{
		if (_isHost)
			NetworkManager.Instance.PeerConnected -= OnPeerJoined;
	}
}
