using Godot;
using MahjongRising.code.Session;

namespace MahjongRising.code.UI;

/// <summary>
/// 主菜单。挂载到 MainMenu 场景的根 Control 节点。
/// </summary>
public partial class MainMenuUI : Control
{
    public override void _Ready()
    {
        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.Center);
        root.GrowHorizontal = GrowDirection.Both;
        root.GrowVertical = GrowDirection.Both;
        root.AddThemeConstantOverride("separation", 16);
        AddChild(root);

        var title = new Label { Text = "Mahjong Rising", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 48);
        root.AddChild(title);
        root.AddChild(new HSeparator());

        AddBtn(root, "单人游戏 (vs AI)", () =>
        {
            var config = new RoomConfig { Mode = "solo", PlayerCount = 4, AiCount = 3, AiDifficulty = "normal" };
            RoomManager.Instance.CreateSoloRoom(config);
            // 设置 3 个 AI
            for (int i = 1; i <= 3; i++) RoomManager.Instance.AddAi(i, "normal");
            GetTree().ChangeSceneToFile("res://scenes/room_lobby.tscn");
        });

        AddBtn(root, "创建房间 (多人)", () =>
        {
            var config = new RoomConfig { Mode = "host", PlayerCount = 4, AiCount = 0 };
            RoomManager.Instance.CreateHostRoom(config);
            GetTree().ChangeSceneToFile("res://scenes/room_lobby.tscn");
        });

        AddBtn(root, "加入房间", () =>
        {
            // 简单实现：弹出输入框，输入 IP 地址
            var dialog = new AcceptDialog { Title = "输入服务器地址", DialogText = "输入 IP:端口" };
            var input = new LineEdit { PlaceholderText = "127.0.0.1:7777", CustomMinimumSize = new Vector2(300, 0) };
            dialog.AddChild(input);
            dialog.Confirmed += () =>
            {
                var parts = input.Text.Split(':');
                string addr = parts[0];
                int port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 7777;
                RoomManager.Instance.JoinRoom(addr, port);
                GetTree().ChangeSceneToFile("res://scenes/game_board.tscn"); // 客户端直接进入等待
            };
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(400, 150));
        });

        AddBtn(root, "退出", () => GetTree().Quit());
    }

    private static void AddBtn(Container parent, string text, System.Action action)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(300, 50) };
        btn.Pressed += action;
        parent.AddChild(btn);
    }
}
