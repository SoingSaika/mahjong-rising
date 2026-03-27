using System;
using System.Collections.Generic;

namespace MahjongRising.code.Game.Rpc;

// ══════════════════════════════════════════════════
// 所有 RPC 传输用的 DTO（Data Transfer Object）
// 设计原则：最小同步量，只传差异和 ID
//
// 网络开销分析：
//   - TileDto:         ~40 字节（GUID + tileCode + bool）
//   - 一次弃牌广播:     ~80 字节 × 4 人 = 320 字节
//   - 一次反应窗口:     ~200 字节（只发给有动作的人）
//   - 初始配牌:         ~40 × 13 × 1 = 520 字节/人（只发自己的牌）
//   - 全局状态重同步:   ~2KB（仅断线重连时用）
// ══════════════════════════════════════════════════

// ── 基础 DTO ──

/// <summary>牌的精简序列化（只传编码 + 实例 ID）。</summary>
public class TileDto
{
    public string InstanceId { get; set; } = "";
    public string TileCode { get; set; } = "";
    public bool IsFaceUp { get; set; }
}

/// <summary>副露组的 DTO。</summary>
public class MeldDto
{
    public string Kind { get; set; } = "";
    public List<TileDto> Tiles { get; set; } = new();
    public int SourceSeat { get; set; } = -1;
    public bool IsOpen { get; set; } = true;
}

/// <summary>可用动作选项的 DTO。</summary>
public class ActionOptionDto
{
    public string OptionId { get; set; } = "";
    public List<string> InvolvedTileIds { get; set; } = new();
    public Dictionary<string, string> Extra { get; set; } = new();
}

/// <summary>可用动作集合的 DTO。</summary>
public class AvailableActionsDto
{
    public int Seat { get; set; }
    public List<ActionOptionDto>? Chi { get; set; }
    public List<ActionOptionDto>? Peng { get; set; }
    public List<ActionOptionDto>? Gang { get; set; }
    public List<ActionOptionDto>? Hu { get; set; }
    public Dictionary<string, List<ActionOptionDto>> Custom { get; set; } = new();
}

/// <summary>单个玩家的精简信息（用于初始同步、重连）。</summary>
public class PlayerInfoDto
{
    public int Seat { get; set; }
    public string CharacterId { get; set; } = "";
    public int Score { get; set; }
    public int SeatWind { get; set; }
    public bool IsRiichi { get; set; }
    public int HandCount { get; set; }           // 其他人只看牌数
    public List<TileDto>? HandTiles { get; set; } // 自己才有牌面
    public List<MeldDto> Melds { get; set; } = new();
    public List<TileDto> Discards { get; set; } = new();
}

// ══════════════════════════════════════════════════
// Server → Client 事件
// ══════════════════════════════════════════════════

/// <summary>
/// 开局初始化同步。
/// 每位玩家收到的版本不同：只有自己的手牌是明牌。
/// 这是整局中传输量最大的一次，约 520 字节/人。
/// </summary>
public class GameInitEventDto
{
    public int MySeat { get; set; }
    public int DealerSeat { get; set; }
    public int RoundWind { get; set; }              // 0=东 1=南 2=西 3=北
    public int Honba { get; set; }
    public int RiichiSticks { get; set; }
    public int WallRemaining { get; set; }
    public List<TileDto> DoraIndicators { get; set; } = new(); // 宝牌指示牌
    public string TableThemeId { get; set; } = "";
    public string? TileSkinId { get; set; }
    public List<PlayerInfoDto> Players { get; set; } = new();
}

/// <summary>某人摸牌。摸牌者收到明牌，其他人只收到 hidden。</summary>
public class DrawEventDto
{
    public int Seat { get; set; }
    public TileDto? Tile { get; set; }
    public int WallRemaining { get; set; }
    public bool IsRinshan { get; set; }             // 是否岭上摸牌
}

/// <summary>某人弃牌。全员可见。</summary>
public class DiscardEventDto
{
    public int Seat { get; set; }
    public TileDto Tile { get; set; } = new();
    public bool IsRiichi { get; set; }              // 是否同时宣告立直
    public bool IsTsumogiri { get; set; }           // 是否摸切
}

/// <summary>
/// 立直宣告通知（独立于弃牌，因为需要：放立直棒动画 + 分数扣减 + 一发标记）。
/// 弃牌 DTO 也带 IsRiichi 标记，此通知用于确认服务器已处理完毕的状态变更。
/// </summary>
public class RiichiEventDto
{
    public int Seat { get; set; }
    public int NewScore { get; set; }               // 扣完 1000 后的分数
    public int RiichiSticksOnTable { get; set; }    // 场上总立直棒数
    public bool IsDoubleRiichi { get; set; }
}

/// <summary>反应窗口打开。只发给有可用动作的玩家。</summary>
public class ReactionWindowEventDto
{
    public TileDto SourceTile { get; set; } = new();
    public int SourceSeat { get; set; }
    public float TimeoutSeconds { get; set; }
    public AvailableActionsDto AvailableActions { get; set; } = new();
}

/// <summary>反应结算结果。全员广播。</summary>
public class ReactionResultEventDto
{
    public int WinnerSeat { get; set; }
    public string ActionType { get; set; } = "";
    public string OptionId { get; set; } = "";
    public MeldDto? NewMeld { get; set; }
    /// <summary>从赢家手牌中移除的牌 ID 列表（客户端同步手牌用）。</summary>
    public List<string> RemovedTileIds { get; set; } = new();
    /// <summary>来源座位（被吃/碰的牌的原主人）。</summary>
    public int SourceSeat { get; set; } = -1;
    /// <summary>来源牌 ID（从弃牌区移走的那张）。</summary>
    public string SourceTileId { get; set; } = "";
}

/// <summary>回合开始。全员广播。</summary>
public class TurnStartEventDto
{
    public int Seat { get; set; }
    public int TurnCount { get; set; }
    public string Phase { get; set; } = "";         // 当前阶段字符串
}

/// <summary>新宝牌指示牌翻开（杠后）。全员广播。</summary>
public class DoraRevealEventDto
{
    public TileDto NewIndicator { get; set; } = new();
    public int TotalDoraCount { get; set; }
}

/// <summary>分数变动通知。全员广播。仅传变化量。</summary>
public class ScoreUpdateEventDto
{
    /// <summary>seat → 变化量（正=得分，负=失分）。</summary>
    public Dictionary<int, int> Changes { get; set; } = new();
    public string Reason { get; set; } = "";        // "riichi"/"win"/"penalty"
}

/// <summary>
/// 局结束。全员广播。包含完整结算信息。
/// </summary>
public class RoundEndEventDto
{
    public string Reason { get; set; } = "";        // "tsumo"/"ron"/"draw"/"abort"
    public int? WinnerSeat { get; set; }
    public int? LoserSeat { get; set; }             // 放铳者（ron 时）
    public List<string> YakuIds { get; set; } = new();
    /// <summary>役名列表（日文显示名，与 YakuIds 一一对应）。</summary>
    public List<string> YakuNames { get; set; } = new();
    public int TotalHan { get; set; }
    public int Fu { get; set; }
    public int BasePoints { get; set; }
    public bool IsYakuman { get; set; }
    public Dictionary<int, int> ScoreChanges { get; set; } = new();
    public List<TileDto>? UraDoraIndicators { get; set; } // 里宝牌（立直和了时翻开）
    public List<TileDto>? WinnerHand { get; set; }  // 和了者手牌（结算画面展示）
}

/// <summary>阶段变更通知。全员广播。用于客户端 UI 状态机同步。</summary>
public class PhaseChangeEventDto
{
    public string Phase { get; set; } = "";
    public int CurrentSeat { get; set; }
}

/// <summary>自摸可用动作通知。只发给当前玩家。</summary>
public class SelfActionsEventDto
{
    public AvailableActionsDto AvailableActions { get; set; } = new();
}

/// <summary>
/// 全状态重同步（断线重连用）。只发给重连的那个人。
/// 这是唯一一次发送大量数据的 RPC（约 2KB）。
/// </summary>
public class FullSyncEventDto
{
    public GameInitEventDto InitState { get; set; } = new();
    public string CurrentPhase { get; set; } = "";
    public int CurrentTurnSeat { get; set; }
    public int TurnCount { get; set; }
    public bool IsReactionWindowOpen { get; set; }
}

// ══════════════════════════════════════════════════
// Client → Server 请求
// ══════════════════════════════════════════════════

/// <summary>客户端请求弃牌。</summary>
public class DiscardRequestDto
{
    public string TileInstanceId { get; set; } = "";
    public bool IsRiichi { get; set; }
}

/// <summary>客户端提交反应（吃/碰/杠/胡/跳过）。</summary>
public class ReactionRequestDto
{
    public string ActionType { get; set; } = "";
    public string OptionId { get; set; } = "";
}

/// <summary>客户端请求自摸动作（暗杠/加杠/自摸胡）。</summary>
public class SelfActionRequestDto
{
    public string ActionType { get; set; } = "";
    public string OptionId { get; set; } = "";
}

/// <summary>客户端选择角色（开局前）。</summary>
public class CharacterSelectRequestDto
{
    public string CharacterId { get; set; } = "";
}

/// <summary>客户端表示准备就绪。</summary>
public class ReadyRequestDto
{
    public bool IsReady { get; set; }
}