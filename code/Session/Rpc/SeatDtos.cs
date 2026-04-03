using System.Collections.Generic;

namespace MahjongRising.code.Session.Rpc;

/// <summary>服务端通知某客户端其座位号 + 当前完整座位表。</summary>
public class SeatAssignmentDto
{
    public int YourSeat { get; set; }
    public List<SeatInfoDto> Seats { get; set; } = new();
}

/// <summary>广播给所有人的座位表更新。</summary>
public class SeatUpdateDto
{
    public List<SeatInfoDto> Seats { get; set; } = new();
}

/// <summary>单个座位的状态信息。</summary>
public class SeatInfoDto
{
    public int Seat { get; set; }
    /// <summary>"human" / "ai" / "empty"</summary>
    public string Status { get; set; } = "empty";
    public long PeerId { get; set; }
}