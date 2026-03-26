using Godot;

namespace MahjongRising.code.Session;

/// <summary>
/// 房间管理器。处理开房/加入/开始，管理场景切换。
/// 挂载为 AutoLoad。
/// </summary>
public partial class RoomManager : Node
{
    public static RoomManager Instance { get; private set; } = null!;
    public GameSession? CurrentSession { get; private set; }
    public RoomConfig? CurrentConfig { get; set; }

    public override void _Ready() { Instance = this; }

    /// <summary>单人模式。</summary>
    public GameSession CreateSoloRoom(RoomConfig config)
    {
        NetworkManager.Instance.HostSolo();
        CurrentConfig = config;
        var session = CreateSession(config);
        session.SetHumanPlayer(0, 1); // 本地玩家 seat=0, peerId=1
        return session;
    }

    /// <summary>多人 - 开房。房主是 peerId=1。</summary>
    public GameSession CreateHostRoom(RoomConfig config, int port = 7777)
    {
        var err = NetworkManager.Instance.HostGame(port, config.PlayerCount);
        if (err != Error.Ok) return null!;
        CurrentConfig = config;
        var session = CreateSession(config);
        session.SetHumanPlayer(0, 1); // 房主 seat=0, peerId=1
        return session;
    }

    /// <summary>多人 - 加入他人房间。</summary>
    public Error JoinRoom(string addr, int port = 7777)
    {
        return NetworkManager.Instance.JoinGame(addr, port);
    }

    private GameSession CreateSession(RoomConfig config)
    {
        CurrentSession?.EndGame();
        var session = new GameSession { Config = config };
        AddChild(session);
        CurrentSession = session;
        return session;
    }

    /// <summary>向当前房间添加 AI。</summary>
    public void AddAi(int seat, string difficulty = "normal")
    {
        CurrentSession?.SetAiPlayer(seat, difficulty);
    }

    /// <summary>开始当前房间的游戏。切换到游戏场景。</summary>
    public void StartGame()
    {
        CurrentSession?.StartGame();
    }

    public void LeaveRoom()
    {
        CurrentSession?.EndGame();
        CurrentSession = null;
        NetworkManager.Instance.Disconnect();
    }
}