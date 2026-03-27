using Godot;
using MahjongRising.code.Session;

namespace MahjongRising.code.UI;

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

        Btn(root, "单人游戏 (vs AI)", () =>
        {
            var config = new RoomConfig { Mode = "solo", PlayerCount = 4, AiCount = 3, AiDifficulty = "normal" };
            RoomManager.Instance.CreateSoloRoom(config);
            for (int i = 1; i <= 3; i++) RoomManager.Instance.AddAi(i, "normal");
            GetTree().ChangeSceneToFile("res://scenes/RoomLobby.tscn");
        });

        Btn(root, "创建房间 (多人)", () =>
        {
            var config = new RoomConfig { Mode = "host", PlayerCount = 4, AiCount = 0 };
            RoomManager.Instance.CreateHostRoom(config);
            GetTree().ChangeSceneToFile("res://scenes/RoomLobby.tscn");
        });

        Btn(root, "加入房间", () =>
        {
            var dialog = new AcceptDialog { Title = "输入服务器地址" };
            var input = new LineEdit { PlaceholderText = "127.0.0.1:7777", CustomMinimumSize = new Vector2(300, 0) };
            dialog.AddChild(input);
            dialog.Confirmed += () =>
            {
                var parts = input.Text.Split(':');
                string addr = parts.Length > 0 ? parts[0] : "127.0.0.1";
                int port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 7777;
                var err = RoomManager.Instance.JoinRoom(addr, port);
                if (err == Error.Ok)
                    GetTree().ChangeSceneToFile("res://scenes/RoomLobby.tscn");
            };
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(400, 150));
        });

        Btn(root, "退出", () => GetTree().Quit());
    }

    private static void Btn(Container p, string text, System.Action action)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(300, 50) };
        b.Pressed += action;
        p.AddChild(b);
    }
}