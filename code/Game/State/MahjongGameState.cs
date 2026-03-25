using System.Collections.Generic;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.State;

public class MahjongGameState
{
    // ── 玩家 ──
    public List<PlayerState> Players { get; } = new();

    // ── 牌区 ──
    public List<MahjongTileState> Wall { get; } = new();
    public List<MahjongTileState> DeadWall { get; } = new();
    public List<MahjongTileState> RevealedTiles { get; } = new();

    // ── 回合控制 ──
    public int CurrentTurnSeat { get; set; } = 0;
    public int DealerSeat { get; set; } = 0;
    public int TurnCount { get; set; } = 0;
    public TurnPhase Phase { get; set; } = TurnPhase.RoundStart;

    // ── 反应窗口（碰/吃/杠/胡） ──
    public ReactionWindow ReactionWindow { get; set; } = new();

    // ── 局信息 ──
    public int RoundWind { get; set; } = 0;   // 0=东 1=南 2=西 3=北
    public int Honba { get; set; } = 0;        // 本场数
    public int RiichiSticks { get; set; } = 0;  // 供托（场上立直棒数）
    public bool IsRoundEnded { get; set; }

    // ── Mod 用的全局运行时标签/计数器 ──
    public HashSet<string> RuntimeTags { get; } = new();
    public Dictionary<string, int> Counters { get; } = new();

    // ── 事件日志（用于回放 & Mod 钩子） ──
    public List<GameEvent> EventLog { get; } = new();

    public PlayerState GetPlayer(int seat) => Players[seat];

    public int NextSeat(int seat) => (seat + 1) % Players.Count;

    public int PlayerCount => Players.Count;
}