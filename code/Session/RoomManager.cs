using Godot;
using MahjongRising.code.Game;

namespace MahjongRising.code.Session;

/// <summary>
/// 房间管理器。
/// 负责创建房间、管理大厅状态、处理玩家加入/离开。
/// 通常挂载在主菜单场景或作为 AutoLoad。
///
/// 使用示例：
///
///   // 单人 AI 对局
///   var session = roomManager.CreateSoloRoom(new RoomConfig { AiDifficulty = "hard" });
///   session.Start();
///
///   // 多人开房
///   var session = roomManager.CreateHostRoom(new RoomConfig { AiCount = 1 });
///   // 等待玩家通过 Godot Multiplayer 连接...
///   session.AddHumanPlayer(peerId, seat);
///   // 全员就绪后
///   session.Start();
///
///   // 加入他人房间
///   roomManager.JoinRoom(address, port);
///   // 服务器会创建 session 并同步
/// </summary>
public partial class RoomManager : Node
{
    private GameBootstrap _bootstrap = null!;
    private GameSession? _currentSession;

    public GameSession? CurrentSession => _currentSession;

    public override void _Ready()
    {
        // 从 AutoLoad 获取 Bootstrap
        _bootstrap = GetNode<GameBootstrap>("/root/GameBootstrap");
    }

    // ═══════════════════════════════════
    // 单人模式
    // ═══════════════════════════════════

    /// <summary>
    /// 创建单人 AI 对局。
    /// 本地既是服务器也是客户端，3 个 AI 对手。
    /// </summary>
    public GameSession CreateSoloRoom(RoomConfig? config = null)
    {
        config ??= new RoomConfig
        {
            Mode = "solo",
            PlayerCount = 4,
            AiCount = 3,
            AiDifficulty = "normal"
        };

        // 单人模式：创建本地 ENet peer 作为服务器
        var peer = new ENetMultiplayerPeer();
        peer.CreateServer(0); // 端口 0 = 不监听外部连接
        Multiplayer.MultiplayerPeer = peer;

        var session = CreateSession(config);
        session.AddLocalPlayer();
        return session;
    }

    // ═══════════════════════════════════
    // 多人模式 - 房主
    // ═══════════════════════════════════

    /// <summary>
    /// 创建多人房间（作为服务器）。
    /// 玩家通过 Multiplayer API 连接后调用 session.AddHumanPlayer()。
    /// </summary>
    public GameSession CreateHostRoom(RoomConfig config, int port = 7777)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(port, config.PlayerCount);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[RoomManager] 创建服务器失败: {err}");
            return null!;
        }
        Multiplayer.MultiplayerPeer = peer;

        GD.Print($"[RoomManager] 服务器已创建，端口 {port}");

        var session = CreateSession(config);
        // 房主自己是 seat 0
        session.AddHumanPlayer(1, 0); // peerId=1 是服务器自身
        return session;
    }

    // ═══════════════════════════════════
    // 多人模式 - 加入
    // ═══════════════════════════════════

    /// <summary>
    /// 加入他人的房间（作为客户端）。
    /// 连接成功后，服务器会发送 GameInit 同步状态。
    /// </summary>
    public void JoinRoom(string address, int port = 7777)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(address, port);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[RoomManager] 连接失败: {err}");
            return;
        }
        Multiplayer.MultiplayerPeer = peer;

        GD.Print($"[RoomManager] 正在连接 {address}:{port}");

        // 连接成功后服务器会通过 RPC 发送 GameInitEventDto
        // 客户端在收到 NotifyGameInit 后构建本地视图
    }

    // ═══════════════════════════════════
    // 内部
    // ═══════════════════════════════════

    private GameSession CreateSession(RoomConfig config)
    {
        // 清理旧 session
        _currentSession?.Finish();

        var session = new GameSession(config, _bootstrap);
        AddChild(session);
        _currentSession = session;

        // 监听结束信号
        session.GameFinished += OnGameFinished;

        return session;
    }

    private void OnGameFinished()
    {
        GD.Print("[RoomManager] 游戏结束");
        _currentSession = null;
        // 可以在这里切换回主菜单场景
    }

    /// <summary>关闭当前房间。</summary>
    public void LeaveRoom()
    {
        _currentSession?.Finish();
        _currentSession = null;

        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }
    }
}
