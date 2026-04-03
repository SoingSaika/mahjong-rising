using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.AI;

namespace MahjongRising.code.Session.Rpc;

/// <summary>
/// 房间 RPC 管理器。
/// 负责从创建房间到游戏开始之间的所有同步，以及游戏结束后返回大厅。
/// 与 PlayerActionRpcManager（游戏内操作）完全分离。
///
/// 完整多人流程：
///   1. 房主 CreateHostRoom → 服务端创建 GameSession，内含 RoomRpcManager 子节点
///   2. 客户端 JoinRoom → 也创建 GameSession 壳（相同节点树结构）
///   3. Godot 连接成功 → 客户端 Rpc → RequestJoinRoom
///   4. 服务端分配座位 → Rpc → SyncRoomState 广播给全部人
///   5. 房主改 AI / 角色 → Rpc → SyncRoomState 广播
///   6. 房主点开始 → Rpc → NotifyGameStarting → 全部切场景 → 游戏开始
///   7. 游戏结束 → Rpc → NotifyReturnToLobby → 全部回大厅
/// </summary>
public partial class RoomRpcManager : Node
{
    // ── 房间状态（服务端权威） ──
    private readonly RoomSlot[] _slots = new RoomSlot[4];

    // ── 客户端事件（UI 订阅） ──
    public event Action<RoomStateDto>? OnRoomStateUpdated;
    public event Action<int>? OnSeatAssigned;      // 客户端收到自己的座位号
    public event Action? OnGameStarting;            // 游戏即将开始，切场景
    public event Action? OnReturnToLobby;           // 游戏结束，回大厅
    public event Action<string>? OnRoomError;

    public override void _Ready()
    {
        for (int i = 0; i < 4; i++)
            _slots[i] = new RoomSlot { Seat = i };

        // 客户端连接成功后自动请求加入
        Multiplayer.ConnectedToServer += OnConnectedToServer;
    }

    public override void _ExitTree()
    {
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
    }

    private void OnConnectedToServer()
    {
        // 客户端连接成功 → 告诉服务端我要加入
        Rpc(nameof(RequestJoinRoom), "");
    }

    // ═══════════════════════════════════
    // 服务端本地操作（房主调用）
    // ═══════════════════════════════════

    /// <summary>服务端：设置房主。</summary>
    public void ServerSetHost(long peerId)
    {
        _slots[0].PeerId = peerId;
        _slots[0].Status = SlotStatus.Human;
        _slots[0].DisplayName = "房主";
        BroadcastState();
    }

    /// <summary>服务端：添加 AI。</summary>
    public void ServerSetAi(int seat, string difficulty)
    {
        if (seat < 0 || seat >= 4 || seat == 0) return;
        _slots[seat].Status = SlotStatus.Ai;
        _slots[seat].PeerId = AiPlayerAdapter.AiPeerBase + seat;
        _slots[seat].DisplayName = $"AI ({difficulty})";
        _slots[seat].AiDifficulty = difficulty;
        _slots[seat].CharacterId = "";
        BroadcastState();
    }

    /// <summary>服务端：移除 AI（变为空位）。</summary>
    public void ServerRemovePlayer(int seat)
    {
        if (seat < 0 || seat >= 4 || seat == 0) return;
        _slots[seat] = new RoomSlot { Seat = seat };
        BroadcastState();
    }

    /// <summary>服务端：开始游戏。</summary>
    public void ServerStartGame()
    {
        Rpc(nameof(NotifyGameStarting), "");
    }

    /// <summary>服务端：游戏结束，回大厅。</summary>
    public void ServerReturnToLobby()
    {
        Rpc(nameof(NotifyReturnToLobby), "");
    }

    // ═══════════════════════════════════
    // Client → Server
    // ═══════════════════════════════════

    /// <summary>客户端请求加入房间。</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestJoinRoom(string playerName)
    {
        if (!Multiplayer.IsServer()) return;
        long peerId = Multiplayer.GetRemoteSenderId();
        if (peerId == 0) peerId = Multiplayer.GetUniqueId();

        // 已在房间中？
        if (_slots.Any(s => s.PeerId == peerId)) return;

        // 找空位
        for (int i = 1; i < 4; i++)
        {
            if (_slots[i].Status != SlotStatus.Empty) continue;

            _slots[i].PeerId = peerId;
            _slots[i].Status = SlotStatus.Human;
            _slots[i].DisplayName = string.IsNullOrEmpty(playerName) ? $"玩家{peerId}" : playerName;

            GD.Print($"[RoomRpc] Peer {peerId} → Seat {i}");

            // 告诉该客户端座位号
            RpcId(peerId, nameof(NotifySeatAssigned), i);

            // 广播新状态
            BroadcastState();
            return;
        }

        RpcId(peerId, nameof(NotifyRoomError), "房间已满");
    }

    /// <summary>客户端选择角色。</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSelectCharacter(string characterId)
    {
        if (!Multiplayer.IsServer()) return;
        long peerId = Multiplayer.GetRemoteSenderId();
        if (peerId == 0) peerId = Multiplayer.GetUniqueId();

        var slot = _slots.FirstOrDefault(s => s.PeerId == peerId);
        if (slot == null) return;

        slot.CharacterId = characterId;
        BroadcastState();
    }

    /// <summary>客户端切换准备状态。</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestToggleReady()
    {
        if (!Multiplayer.IsServer()) return;
        long peerId = Multiplayer.GetRemoteSenderId();
        if (peerId == 0) peerId = Multiplayer.GetUniqueId();

        var slot = _slots.FirstOrDefault(s => s.PeerId == peerId);
        if (slot == null) return;

        slot.IsReady = !slot.IsReady;
        BroadcastState();
    }

    // ═══════════════════════════════════
    // Server → Client
    // ═══════════════════════════════════

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncRoomState(string json)
    {
        var dto = RpcSerializer.Deserialize<RoomStateDto>(json);
        if (dto != null) OnRoomStateUpdated?.Invoke(dto);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifySeatAssigned(int seat)
    {
        OnSeatAssigned?.Invoke(seat);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyGameStarting(string _)
    {
        OnGameStarting?.Invoke();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyReturnToLobby(string _)
    {
        OnReturnToLobby?.Invoke();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyRoomError(string msg)
    {
        OnRoomError?.Invoke(msg);
    }

    // ═══════════════════════════════════
    // 状态广播
    // ═══════════════════════════════════

    private void BroadcastState()
    {
        var dto = new RoomStateDto();
        for (int i = 0; i < 4; i++)
            dto.Slots.Add(new RoomSlotDto
            {
                Seat = _slots[i].Seat,
                Status = _slots[i].Status.ToString(),
                DisplayName = _slots[i].DisplayName,
                CharacterId = _slots[i].CharacterId,
                IsReady = _slots[i].IsReady
            });
        Rpc(nameof(SyncRoomState), RpcSerializer.Serialize(dto));
    }

    /// <summary>获取某座位的 peerId（GameSession.StartGame 用）。</summary>
    public long GetPeerId(int seat) => seat >= 0 && seat < 4 ? _slots[seat].PeerId : 0;
    public SlotStatus GetStatus(int seat) => seat >= 0 && seat < 4 ? _slots[seat].Status : SlotStatus.Empty;
    public string GetAiDifficulty(int seat) => seat >= 0 && seat < 4 ? _slots[seat].AiDifficulty : "normal";

    /// <summary>直接获取当前房间状态（本地调用，不走 RPC）。用于 UI 初始化。</summary>
    public RoomStateDto GetCurrentState()
    {
        var dto = new RoomStateDto();
        for (int i = 0; i < 4; i++)
            dto.Slots.Add(new RoomSlotDto
            {
                Seat = _slots[i].Seat,
                Status = _slots[i].Status.ToString(),
                DisplayName = _slots[i].DisplayName,
                CharacterId = _slots[i].CharacterId,
                IsReady = _slots[i].IsReady
            });
        return dto;
    }
}

// ── 服务端内部状态 ──

public class RoomSlot
{
    public int Seat { get; init; }
    public long PeerId { get; set; }
    public SlotStatus Status { get; set; } = SlotStatus.Empty;
    public string DisplayName { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public string AiDifficulty { get; set; } = "normal";
    public bool IsReady { get; set; }
}

public enum SlotStatus { Empty, Human, Ai }

// ── DTO ──

public class RoomStateDto
{
    public List<RoomSlotDto> Slots { get; set; } = new();
}

public class RoomSlotDto
{
    public int Seat { get; set; }
    public string Status { get; set; } = "Empty";
    public string DisplayName { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public bool IsReady { get; set; }
}