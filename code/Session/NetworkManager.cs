using System;
using Godot;

namespace MahjongRising.code.Session;

/// <summary>
/// 网络传输管理器。
///
/// 架构：房主创建本地服务端，然后作为客户端加入。
///   - 服务端逻辑在 IsServer()=true 时运行（peerId=1）
///   - 房主的客户端视角与其他玩家完全一致，通过 RPC 接收通知
///   - 后期切换 Steam P2P 只需替换 CreateTransport() 实现
///
/// 使用方式：
///   NetworkManager.Instance.HostGame(7777);     // 创建服务端 + 自己加入
///   NetworkManager.Instance.JoinGame(addr, 7777); // 加入他人
///   NetworkManager.Instance.HostSolo();          // 单人模式（本地服务端，不监听外部）
/// </summary>
public partial class NetworkManager : Node
{
    public static NetworkManager Instance { get; private set; } = null!;

    [Signal] public delegate void PeerConnectedEventHandler(long peerId);
    [Signal] public delegate void PeerDisconnectedEventHandler(long peerId);
    [Signal] public delegate void ConnectedToServerEventHandler();
    [Signal] public delegate void ConnectionFailedEventHandler();
    [Signal] public delegate void ServerDisconnectedEventHandler();

    /// <summary>当前传输类型。</summary>
    public TransportType CurrentTransport { get; private set; } = TransportType.None;

    /// <summary>本机的 peer ID。服务端=1。</summary>
    public long MyPeerId => Multiplayer.GetUniqueId();

    /// <summary>是否是服务端（含 solo 模式）。</summary>
    public bool IsServer => Multiplayer.IsServer();

    /// <summary>是否已连接。</summary>
    public bool IsConnected => Multiplayer.MultiplayerPeer?.GetConnectionStatus()
        == MultiplayerPeer.ConnectionStatus.Connected;

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    // ═══════════════════════════════════
    // 公开 API
    // ═══════════════════════════════════

    /// <summary>
    /// 单人模式：创建本地服务端，不监听外部连接。
    /// 房主 peerId = 1。
    /// </summary>
    public Error HostSolo()
    {
        var peer = new ENetMultiplayerPeer();
        // 端口 0 + maxClients 0 = 不监听
        var err = peer.CreateServer(0, 1);
        if (err != Error.Ok) { GD.PrintErr($"[Network] Solo 创建失败: {err}"); return err; }

        Multiplayer.MultiplayerPeer = peer;
        CurrentTransport = TransportType.LocalOnly;
        GD.Print("[Network] 单人模式，本地服务端已创建");
        return Error.Ok;
    }

    /// <summary>
    /// 多人模式 - 房主：创建服务端并监听指定端口。
    /// 房主自身作为 peerId=1 的客户端参与游戏。
    /// 其他玩家连接后获得 peerId 2, 3, ...
    /// </summary>
    public Error HostGame(int port, int maxPlayers = 4)
    {
        var peer = CreateTransport();
        var err = peer.CreateServer(port, maxPlayers);
        if (err != Error.Ok) { GD.PrintErr($"[Network] 服务端创建失败: {err}"); return err; }

        Multiplayer.MultiplayerPeer = peer;
        CurrentTransport = TransportType.ENet;
        GD.Print($"[Network] 服务端已创建，端口 {port}，房主 peerId=1");
        return Error.Ok;
    }

    /// <summary>
    /// 多人模式 - 加入：连接到指定服务端。
    /// </summary>
    public Error JoinGame(string address, int port)
    {
        var peer = CreateTransport();
        var err = peer.CreateClient(address, port);
        if (err != Error.Ok) { GD.PrintErr($"[Network] 连接失败: {err}"); return err; }

        Multiplayer.MultiplayerPeer = peer;
        CurrentTransport = TransportType.ENet;
        GD.Print($"[Network] 正在连接 {address}:{port}");
        return Error.Ok;
    }

    /// <summary>断开连接并重置。</summary>
    public void Disconnect()
    {
        Multiplayer.MultiplayerPeer?.Close();
        Multiplayer.MultiplayerPeer = null;
        CurrentTransport = TransportType.None;
        GD.Print("[Network] 已断开");
    }

    // ═══════════════════════════════════
    // 传输层工厂（Steam P2P 预留点）
    // ═══════════════════════════════════

    /// <summary>
    /// 创建传输层 peer。
    /// 后期接入 Steam 时，在此返回 SteamMultiplayerPeer。
    /// </summary>
    private static ENetMultiplayerPeer CreateTransport()
    {
        // TODO: 根据配置返回 SteamMultiplayerPeer
        // if (UseSteam) return new SteamMultiplayerPeer();
        return new ENetMultiplayerPeer();
    }

    // ═══════════════════════════════════
    // 事件转发
    // ═══════════════════════════════════

    private void OnPeerConnected(long id) { GD.Print($"[Network] Peer 连接: {id}"); EmitSignal(SignalName.PeerConnected, id); }
    private void OnPeerDisconnected(long id) { GD.Print($"[Network] Peer 断开: {id}"); EmitSignal(SignalName.PeerDisconnected, id); }
    private void OnConnectedToServer() { GD.Print($"[Network] 已连接到服务端，我的 peerId={MyPeerId}"); EmitSignal(SignalName.ConnectedToServer); }
    private void OnConnectionFailed() { GD.PrintErr("[Network] 连接失败"); EmitSignal(SignalName.ConnectionFailed); }
    private void OnServerDisconnected() { GD.Print("[Network] 服务端断开"); EmitSignal(SignalName.ServerDisconnected); }
}

public enum TransportType { None, LocalOnly, ENet, SteamP2P }
