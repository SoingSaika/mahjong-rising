using System;
using Godot;

namespace MahjongRising.code.Session.Rpc;

/// <summary>
/// PlayerActionRpcManager 的补充 RPC 方法：座位同步。
/// 作为 partial class 合并到 PlayerActionRpcManager。
/// </summary>
public partial class GameSessionRpc : Node
{
    /// <summary>客户端收到座位分配。</summary>
    public event Action<SeatAssignmentDto>? OnSeatAssignment;

    /// <summary>所有人收到座位表更新。</summary>
    public event Action<SeatUpdateDto>? OnSeatUpdate;

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifySeatAssignment(string json)
    {
        var dto = RpcSerializer.Deserialize<SeatAssignmentDto>(json);
        if (dto != null)
        {
            GD.Print($"[Seat] 收到座位分配: seat={dto.YourSeat}");
            OnSeatAssignment?.Invoke(dto);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifySeatUpdate(string json)
    {
        var dto = RpcSerializer.Deserialize<SeatUpdateDto>(json);
        if (dto != null)
        {
            OnSeatUpdate?.Invoke(dto);
        }
    }
}