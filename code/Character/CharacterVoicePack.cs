using System.Collections.Generic;

namespace MahjongRising.code.Character;

/// <summary>
/// 角色语音包。
/// 定义对局中各种事件触发的语音文件路径。
/// 每个事件可以配置多条语音，游戏随机选取。
/// </summary>
public class CharacterVoicePack
{
    /// <summary>
    /// 语音行映射。
    /// 键 = 事件/场景 ID，值 = 该场景的多条可选语音路径。
    /// </summary>
    public Dictionary<string, List<string>> VoiceLines { get; init; } = new();

    /// <summary>全局音量倍率（0.0 ~ 2.0）。</summary>
    public float VolumeScale { get; set; } = 1.0f;

    /// <summary>语音语言标识（"ja" / "zh" / "en"）。</summary>
    public string Language { get; set; } = "ja";

    // ── 内置事件 ID 常量 ──

    /// <summary>宣告吃。</summary>
    public const string CallChi = "call_chi";
    /// <summary>宣告碰。</summary>
    public const string CallPeng = "call_peng";
    /// <summary>宣告杠。</summary>
    public const string CallGang = "call_gang";
    /// <summary>宣告立直。</summary>
    public const string CallRiichi = "call_riichi";
    /// <summary>宣告自摸。</summary>
    public const string CallTsumo = "call_tsumo";
    /// <summary>宣告荣和。</summary>
    public const string CallRon = "call_ron";
    /// <summary>弃牌时的随机一言。</summary>
    public const string Discard = "discard";
    /// <summary>轮到自己时。</summary>
    public const string TurnStart = "turn_start";
    /// <summary>胜利。</summary>
    public const string Win = "win";
    /// <summary>失败。</summary>
    public const string Lose = "lose";
    /// <summary>流局。</summary>
    public const string Draw = "draw";
    /// <summary>被他人立直时。</summary>
    public const string ReactRiichi = "react_riichi";
    /// <summary>被他人和牌时。</summary>
    public const string ReactHu = "react_hu";
    /// <summary>对局开始。</summary>
    public const string GameStart = "game_start";
    /// <summary>空闲等待时的随机语音。</summary>
    public const string Idle = "idle";

    // ── 便捷方法 ──

    /// <summary>添加一条语音行。</summary>
    public void AddLine(string eventId, string audioPath)
    {
        if (!VoiceLines.ContainsKey(eventId))
            VoiceLines[eventId] = new List<string>();
        VoiceLines[eventId].Add(audioPath);
    }

    /// <summary>获取某事件的随机语音路径，无则返回 null。</summary>
    public string? GetRandomLine(string eventId, System.Random? rng = null)
    {
        if (!VoiceLines.TryGetValue(eventId, out var lines) || lines.Count == 0)
            return null;
        rng ??= new System.Random();
        return lines[rng.Next(lines.Count)];
    }

    /// <summary>
    /// 用目录约定批量填充标准语音行。
    /// 约定：{basePath}/{eventId}_01.ogg, {eventId}_02.ogg, ...
    /// </summary>
    public void PopulateFromDirectory(string basePath, int linesPerEvent = 3)
    {
        string[] events =
        {
            CallChi, CallPeng, CallGang, CallRiichi, CallTsumo, CallRon,
            Discard, TurnStart, Win, Lose, Draw, ReactRiichi, ReactHu,
            GameStart, Idle
        };

        foreach (var evt in events)
        {
            for (int i = 1; i <= linesPerEvent; i++)
            {
                AddLine(evt, $"{basePath}/{evt}_{i:D2}.ogg");
            }
        }
    }
}
