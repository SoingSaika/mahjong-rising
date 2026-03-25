using System.Collections.Generic;
using MahjongRising.code.Mahjong.States;

namespace MahjongRising.code.Player.States;

public class PlayerState
{
    public int Seat { get; init; }

    // ── 牌区 ──
    public List<MahjongTileState> Hand { get; } = new();
    public List<MahjongTileState> Discards { get; } = new();
    public List<MeldGroup> Melds { get; } = new();   // 改为结构化副露组

    // ── 分数 ──
    public int Score { get; set; } = 25000;

    // ── 立直相关 ──
    public bool IsRiichi { get; set; }
    public bool IsDoubleRiichi { get; set; }
    public bool IsIppatsu { get; set; }           // 一发

    // ── 状态标记 ──
    public bool HasDrawnThisTurn { get; set; }    // 本巡是否已摸牌
    public bool IsMenzen { get; set; } = true;    // 门前清（无明副露）
    public int SeatWind { get; set; }             // 0=东 1=南 2=西 3=北

    // ── Mod 运行时标签/计数器 ──
    public HashSet<string> RuntimeTags { get; } = new();
    public Dictionary<string, int> Counters { get; } = new();

    // ── 网络 ──
    public long PeerId { get; set; }              // Godot Multiplayer peer id
    public bool IsConnected { get; set; } = true;

    public PlayerState(int seat)
    {
        Seat = seat;
    }
}