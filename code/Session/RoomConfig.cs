using System.Collections.Generic;

namespace MahjongRising.code.Session;

/// <summary>
/// 房间配置。创建房间时指定，整局不变。
/// </summary>
public class RoomConfig
{
    /// <summary>"solo" / "host" / "join"</summary>
    public string Mode { get; init; } = "solo";

    /// <summary>玩家数量（含 AI，通常 4）。</summary>
    public int PlayerCount { get; init; } = 4;

    /// <summary>AI 玩家数量（单人模式 = PlayerCount - 1，多人时可补 AI）。</summary>
    public int AiCount { get; init; } = 3;

    /// <summary>AI 难度（"easy" / "normal" / "hard"）。</summary>
    public string AiDifficulty { get; init; } = "normal";

    /// <summary>局数（"east" 东风 4 局 / "south" 半庄 8 局）。</summary>
    public string GameLength { get; init; } = "south";

    /// <summary>是否使用赤宝牌。</summary>
    public bool UseRedDora { get; init; } = true;

    /// <summary>桌面主题 ID。</summary>
    public string TableThemeId { get; init; } = "builtin.classic";

    /// <summary>皮肤包 ID（null = 默认）。</summary>
    public string? TileSkinId { get; init; }

    /// <summary>反应窗口超时秒数。</summary>
    public float ReactionTimeoutSeconds { get; init; } = 10f;

    /// <summary>各座位的角色选择。Key = seat。</summary>
    public Dictionary<int, string> CharacterSelections { get; init; } = new();
}
