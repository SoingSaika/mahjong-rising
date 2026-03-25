using System;
using System.Collections.Generic;

namespace MahjongRising.code.Game.Rules;

/// <summary>
/// 反应窗口：弃牌后打开，收集所有非当前回合玩家的反应（吃/碰/杠/胡/跳过），
/// 超时或全部收到后由 PriorityResolver 决定最终执行的动作。
/// </summary>
public class ReactionWindow
{
    public bool IsOpen { get; set; }

    /// <summary>触发反应窗口的那张牌的 InstanceId。</summary>
    public Guid? SourceTileInstanceId { get; set; }

    /// <summary>打出这张牌的座位。</summary>
    public int? SourceSeat { get; set; }

    /// <summary>窗口打开的 UTC 时间。</summary>
    public DateTime? OpenedAtUtc { get; set; }

    /// <summary>超时秒数（可被 Mod 修改）。</summary>
    public float TimeoutSeconds { get; set; } = 10f;

    /// <summary>
    /// 每个座位的回应。null = 尚未提交, 空列表 = 跳过。
    /// Key = seat, Value = 该座位提交的候选动作列表。
    /// </summary>
    public Dictionary<int, List<PlayerActionCandidate>?> Responses { get; } = new();

    /// <summary>标记某座位跳过。</summary>
    public void SubmitPass(int seat)
    {
        Responses[seat] = new List<PlayerActionCandidate>();
    }

    /// <summary>提交候选动作。</summary>
    public void SubmitCandidate(int seat, PlayerActionCandidate candidate)
    {
        if (!Responses.ContainsKey(seat) || Responses[seat] == null)
            Responses[seat] = new List<PlayerActionCandidate>();
        Responses[seat]!.Add(candidate);
    }

    /// <summary>检查是否所有需要回应的座位都已提交。</summary>
    public bool AllResponded(int playerCount, int sourceSeat)
    {
        for (int i = 0; i < playerCount; i++)
        {
            if (i == sourceSeat) continue;
            if (!Responses.ContainsKey(i) || Responses[i] == null)
                return false;
        }
        return true;
    }

    /// <summary>重置窗口。</summary>
    public void Close()
    {
        IsOpen = false;
        SourceTileInstanceId = null;
        SourceSeat = null;
        OpenedAtUtc = null;
        Responses.Clear();
    }
}

/// <summary>
/// 反应窗口中的候选动作，用于优先级排序。
/// </summary>
public class PlayerActionCandidate
{
    public int Seat { get; init; }
    public string ActionType { get; init; } = "";   // "chi"/"peng"/"gang"/"hu"
    public int Priority { get; init; }               // 数值越大优先级越高
    public Dictionary<string, object> Params { get; init; } = new();
}