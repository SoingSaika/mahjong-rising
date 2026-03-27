using Godot;

namespace MahjongRising.code.Session;

public partial class RoomManager : Node
{
    public static RoomManager Instance { get; private set; } = null!;
    public GameSession? CurrentSession { get; private set; }
    public RoomConfig? CurrentConfig { get; set; }
    public bool IsHost { get; private set; }
    public int MySeat { get; set; } = -1;

    public override void _Ready()
    {
        Instance = this;
        NetworkManager.Instance.PeerConnected += OnPeerConnected;
    }

    public GameSession CreateSoloRoom(RoomConfig config)
    {
        NetworkManager.Instance.HostSolo();
        CurrentConfig = config; IsHost = true; MySeat = 0;
        var s = MakeSession(config); s.SetHumanPlayer(0, 1); return s;
    }

    public GameSession CreateHostRoom(RoomConfig config, int port = 7777)
    {
        var err = NetworkManager.Instance.HostGame(port, config.PlayerCount);
        if (err != Error.Ok) return null!;
        CurrentConfig = config; IsHost = true; MySeat = 0;
        var s = MakeSession(config); s.SetHumanPlayer(0, 1); return s;
    }

    public Error JoinRoom(string addr, int port = 7777)
    {
        var err = NetworkManager.Instance.JoinGame(addr, port);
        if (err != Error.Ok) return err;
        CurrentConfig = new RoomConfig { Mode = "join", PlayerCount = 4 };
        IsHost = false; MySeat = -1;
        MakeSession(CurrentConfig);
        return Error.Ok;
    }

    public void AddAi(int seat, string diff = "normal") => CurrentSession?.SetAiPlayer(seat, diff);
    public void RemoveAi(int seat) => CurrentSession?.RemoveAi(seat);
    public void StartGame() { if (IsHost) CurrentSession?.StartGame(); }

    private void OnPeerConnected(long peerId)
    {
        if (!IsHost || CurrentSession == null) return;
        for (int i = 1; i < (CurrentConfig?.PlayerCount ?? 4); i++)
        {
            var gs = CurrentSession.GameState;
            if (gs.Players.Count <= i) continue;
            var p = gs.Players[i];
            if (p.PeerId > 0 && p.PeerId < 100000) continue;
            CurrentSession.SetHumanPlayer(i, peerId);
            GD.Print($"[Room] Peer {peerId} -> Seat {i}");
            return;
        }
    }

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