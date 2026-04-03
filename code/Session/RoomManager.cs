using Godot;

namespace MahjongRising.code.Session;

/// <summary>
/// 房间管理器（AutoLoad）。
/// 创建 GameSession（含 RoomRpcManager + PlayerActionRpcManager），
/// 房间操作全部通过 RoomRpcManager 的 RPC 同步。
/// </summary>
public partial class RoomManager : Node
{
    public static RoomManager Instance { get; private set; } = null!;
    public GameSession? CurrentSession { get; private set; }
    public RoomConfig? CurrentConfig { get; set; }
    public bool IsHost { get; private set; }
    public int MySeat { get; set; } = -1;

    public override void _Ready() { Instance = this; }

    // ═══ 单人 ═══

    public GameSession CreateSoloRoom(RoomConfig config)
    {
        NetworkManager.Instance.HostSolo();
        CurrentConfig = config; IsHost = true; MySeat = 0;
        var s = MakeSession(config);
        s.RoomRpc.ServerSetHost(1);
        // 预设 AI
        for (int i = 1; i < config.PlayerCount; i++)
            s.RoomRpc.ServerSetAi(i, config.AiDifficulty);
        return s;
    }

    // ═══ 多人 - 房主 ═══

    public GameSession CreateHostRoom(RoomConfig config, int port = 7777)
    {
        var err = NetworkManager.Instance.HostGame(port, config.PlayerCount);
        if (err != Error.Ok) return null!;
        CurrentConfig = config; IsHost = true; MySeat = 0;
        var s = MakeSession(config);
        s.RoomRpc.ServerSetHost(1);
        return s;
    }

    // ═══ 多人 - 加入 ═══

    public Error JoinRoom(string addr, int port = 7777)
    {
        var err = NetworkManager.Instance.JoinGame(addr, port);
        if (err != Error.Ok) return err;
        CurrentConfig = new RoomConfig { Mode = "join", PlayerCount = 4 };
        IsHost = false; MySeat = -1;
        var s = MakeSession(CurrentConfig);

        // 客户端收到座位分配时更新 MySeat
        s.RoomRpc.OnSeatAssigned += seat => { MySeat = seat; GD.Print($"[Room] 我的座位: {seat}"); };
        return Error.Ok;
    }

    // ═══ 房主操作（通过 RoomRpc 广播） ═══

    public void AddAi(int seat, string diff = "normal")
    {
        if (!IsHost || CurrentSession == null) return;
        CurrentSession.RoomRpc.ServerSetAi(seat, diff);
    }

    public void RemoveSlot(int seat)
    {
        if (!IsHost || CurrentSession == null) return;
        CurrentSession.RoomRpc.ServerRemovePlayer(seat);
    }

    public void StartGame()
    {
        if (!IsHost || CurrentSession == null) return;
        // Step 1: broadcast "game starting" → all peers switch to GameBoard scene
        CurrentSession.RoomRpc.ServerStartGame();
        // Step 2 happens in GameBoardUI.SubscribeRpc → calls BeginGame()
    }

    /// <summary>实际开始游戏逻辑（由 GameBoardUI 在场景加载后调用）。</summary>
    public void BeginGame()
    {
        if (!IsHost || CurrentSession == null) return;
        CurrentSession.StartGame();
    }

    // ═══ 内部 ═══

    private GameSession MakeSession(RoomConfig config)
    {
        CurrentSession?.EndGame();
        var s = new GameSession { Config = config };
        s.Name = "GameSession";
        AddChild(s);
        CurrentSession = s;
        return s;
    }

    public void LeaveRoom()
    {
        CurrentSession?.EndGame(); CurrentSession = null;
        IsHost = false; MySeat = -1; NetworkManager.Instance.Disconnect();
    }
}