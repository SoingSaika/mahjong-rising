using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.Game;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;
using MahjongRising.code.Yaku;

namespace MahjongRising.code.Game.Rpc;

/// <summary>
/// RPC 管理器。
///
/// 核心变更：所有 Server→Client RPC 设置 CallLocal=true。
/// 房主(peerId=1)既运行服务端逻辑，也通过 RPC 接收通知更新客户端视图。
/// 客户端通过 C# event 通知 UI 层。
/// </summary>
public partial class PlayerActionRpcManager : Node
{
    private MahjongGameState _gs = null!;
    private MahjongRuleEngine _eng = null!;
    private readonly Dictionary<int, long> _seatToPeer = new();

    // ═══ 客户端事件（UI 层订阅） ═══
    public event Action<GameInitEventDto>? OnGameInit;
    public event Action<DrawEventDto>? OnDraw;
    public event Action<DiscardEventDto>? OnDiscard;
    public event Action<RiichiEventDto>? OnRiichi;
    public event Action<ReactionWindowEventDto>? OnReactionWindow;
    public event Action<ReactionResultEventDto>? OnReactionResult;
    public event Action<DoraRevealEventDto>? OnDoraReveal;
    public event Action<TurnStartEventDto>? OnTurnStart;
    public event Action<PhaseChangeEventDto>? OnPhaseChange;
    public event Action<SelfActionsEventDto>? OnSelfActions;
    public event Action<ScoreUpdateEventDto>? OnScoreUpdate;
    public event Action<RoundEndEventDto>? OnRoundEnd;
    public event Action<string>? OnError;

    public void Initialize(MahjongGameState gs, MahjongRuleEngine eng)
    {
        _gs = gs; _eng = eng;
        _seatToPeer.Clear();
        foreach (var p in _gs.Players) _seatToPeer[p.Seat] = p.PeerId;
    }

    // ═══════════════════════════════
    // Client → Server
    // ═══════════════════════════════

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDiscard(string json)
    {
        if (!Multiplayer.IsServer()) return;
        int seat = GetSeatByPeer(Multiplayer.GetRemoteSenderId());
        if (seat < 0) return;
        var req = D<DiscardRequestDto>(json); if (req == null) return;

        if (!_eng.IsCurrentPlayer(_gs, seat)) { Err(seat, "不是你的回合"); return; }
        if (_gs.Phase != TurnPhase.DiscardPhase && _gs.Phase != TurnPhase.SelfActionPhase) { Err(seat, "当前阶段不能弃牌"); return; }

        var p = _gs.GetPlayer(seat);
        if (!Guid.TryParse(req.TileInstanceId, out var id)) return;
        var ts = _eng.FindTileInHand(p, id);
        if (ts == null || ts.IsLocked) { Err(seat, "无法弃置该牌"); return; }

        DoDiscard(p, ts, req.IsRiichi);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestReaction(string json)
    {
        if (!Multiplayer.IsServer()) return;
        int seat = GetSeatByPeer(Multiplayer.GetRemoteSenderId());
        if (seat < 0) return;
        var req = D<ReactionRequestDto>(json); if (req == null) return;

        if (_gs.Phase != TurnPhase.ReactionPhase || !_gs.ReactionWindow.IsOpen) return;
        if (seat == _gs.ReactionWindow.SourceSeat) return;

        SubmitReaction(seat, req.ActionType, req.OptionId);
        TryResolveWindow();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSelfAction(string json)
    {
        if (!Multiplayer.IsServer()) return;
        int seat = GetSeatByPeer(Multiplayer.GetRemoteSenderId());
        if (seat < 0) return;
        var req = D<SelfActionRequestDto>(json); if (req == null) return;

        if (!_eng.IsCurrentPlayer(_gs, seat) || _gs.Phase != TurnPhase.SelfActionPhase) return;
        var p = _gs.GetPlayer(seat);
        var last = p.Hand.LastOrDefault(); if (last == null) return;
        var vr = _eng.Validators.ValidateAll(req.ActionType, _gs, p, last);
        if (!vr.IsValid) return;

        DoSelfAction(p, req.ActionType, req.OptionId, vr);
    }

    // ═══════════════════════════════
    // AI 直接入口（不走网络，走同样验证）
    // ═══════════════════════════════

    public void HandleAiDiscard(int seat, string json)
    {
        var req = D<DiscardRequestDto>(json); if (req == null) return;
        if (!_eng.IsCurrentPlayer(_gs, seat)) return;
        if (_gs.Phase != TurnPhase.DiscardPhase && _gs.Phase != TurnPhase.SelfActionPhase) return;
        var p = _gs.GetPlayer(seat);
        if (!Guid.TryParse(req.TileInstanceId, out var id)) return;
        var ts = _eng.FindTileInHand(p, id);
        if (ts == null || ts.IsLocked) return;
        DoDiscard(p, ts, req.IsRiichi);
    }

    public void HandleAiSelfAction(int seat, string json)
    {
        var req = D<SelfActionRequestDto>(json); if (req == null) return;
        if (!_eng.IsCurrentPlayer(_gs, seat) || _gs.Phase != TurnPhase.SelfActionPhase) return;
        var p = _gs.GetPlayer(seat);
        var last = p.Hand.LastOrDefault(); if (last == null) return;
        var vr = _eng.Validators.ValidateAll(req.ActionType, _gs, p, last);
        if (!vr.IsValid) return;
        DoSelfAction(p, req.ActionType, req.OptionId, vr);
    }

    public void HandleAiReaction(int seat, string json)
    {
        var req = D<ReactionRequestDto>(json); if (req == null) return;
        if (_gs.Phase != TurnPhase.ReactionPhase || !_gs.ReactionWindow.IsOpen) return;
        if (seat == _gs.ReactionWindow.SourceSeat) return;
        SubmitReaction(seat, req.ActionType, req.OptionId);
        TryResolveWindow();
    }

    // ═══════════════════════════════
    // Server → Client（CallLocal=true → 房主也收到）
    // ═══════════════════════════════

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyGameInit(string json) { OnGameInit?.Invoke(D<GameInitEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyDraw(string json) { OnDraw?.Invoke(D<DrawEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyDiscard(string json) { OnDiscard?.Invoke(D<DiscardEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyRiichi(string json) { OnRiichi?.Invoke(D<RiichiEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyReactionWindow(string json) { OnReactionWindow?.Invoke(D<ReactionWindowEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyReactionResult(string json) { OnReactionResult?.Invoke(D<ReactionResultEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyDoraReveal(string json) { OnDoraReveal?.Invoke(D<DoraRevealEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyTurnStart(string json) { OnTurnStart?.Invoke(D<TurnStartEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyPhaseChange(string json) { OnPhaseChange?.Invoke(D<PhaseChangeEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifySelfActions(string json) { OnSelfActions?.Invoke(D<SelfActionsEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyScoreUpdate(string json) { OnScoreUpdate?.Invoke(D<ScoreUpdateEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyRoundEnd(string json) { OnRoundEnd?.Invoke(D<RoundEndEventDto>(json)!); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyError(string msg) { OnError?.Invoke(msg); }

    // ═══════════════════════════════
    // 服务端游戏逻辑
    // ═══════════════════════════════

    public void ServerStartRound()
    {
        _gs.Phase = TurnPhase.RoundStart;
        for (int i = 0; i < _gs.PlayerCount; i++)
        {
            var dto = BuildInitDto(i);
            RpcId(_seatToPeer[i], nameof(NotifyGameInit), S(dto));
        }

        _gs.CurrentTurnSeat = _gs.DealerSeat;
        _gs.Phase = TurnPhase.DrawPhase;
        BroadcastPhase();
        ServerDraw(_gs.DealerSeat);
    }

    public void ServerDraw(int seat)
    {
        if (_gs.Wall.Count == 0) { ServerRoundEnd("draw", null, -1); return; }

        var p = _gs.GetPlayer(seat);
        var ts = _gs.Wall[0]; _gs.Wall.RemoveAt(0);
        ts.Zone = MahjongTileZone.Hand; ts.OwnerSeat = seat;
        p.Hand.Add(ts); p.HasDrawnThisTurn = true;

        // 明牌给摸牌者
        RpcId(_seatToPeer[seat], nameof(NotifyDraw), S(new DrawEventDto { Seat = seat, Tile = Dto(ts, true), WallRemaining = _gs.Wall.Count }));
        // 暗牌给其他人
        string h = S(new DrawEventDto { Seat = seat, Tile = Dto(ts, false), WallRemaining = _gs.Wall.Count });
        foreach (var kv in _seatToPeer.Where(kv => kv.Key != seat)) RpcId(kv.Value, nameof(NotifyDraw), h);

        _ = _eng.TriggerDrawAction(_gs, p, ts);
        _gs.Phase = TurnPhase.SelfActionPhase;
        BroadcastPhase();

        var sa = _eng.GetAvailableSelfActions(_gs, p, ts);
        if (sa.HasAny)
            RpcId(_seatToPeer[seat], nameof(NotifySelfActions), S(new SelfActionsEventDto { AvailableActions = RpcSerializer.ToDto(sa) }));
        // 如果没有自摸动作，客户端 UI 自动进入弃牌模式
    }

    private void DoDiscard(PlayerState p, MahjongTileState ts, bool riichi)
    {
        bool tsumogiri = p.Hand.Count > 0 && p.Hand[^1].InstanceId == ts.InstanceId;
        p.Hand.Remove(ts); ts.Zone = MahjongTileZone.Discard; ts.IsFaceUp = true; p.Discards.Add(ts);

        if (riichi && p.IsMenzen && !p.IsRiichi)
        {
            p.IsRiichi = true; p.IsIppatsu = true; p.Score -= 1000; _gs.RiichiSticks++;
            if (_gs.TurnCount <= _gs.PlayerCount && _gs.Players.All(x => x.Melds.Count == 0)) p.IsDoubleRiichi = true;
            Broadcast(nameof(NotifyRiichi), new RiichiEventDto { Seat = p.Seat, NewScore = p.Score, RiichiSticksOnTable = _gs.RiichiSticks, IsDoubleRiichi = p.IsDoubleRiichi });
        }

        _ = _eng.TriggerDiscardAction(_gs, p, ts);
        _gs.EventLog.Add(new GameEvent { EventType = GameEvent.Discard, Seat = p.Seat, Payload = new() { ["tileCode"] = ts.Tile.TileCode } });

        Broadcast(nameof(NotifyDiscard), new DiscardEventDto { Seat = p.Seat, Tile = Dto(ts), IsRiichi = riichi, IsTsumogiri = tsumogiri });
        OpenWindow(p.Seat, ts);
    }

    private void OpenWindow(int src, MahjongTileState tile)
    {
        _gs.Phase = TurnPhase.ReactionPhase; BroadcastPhase();
        var w = _gs.ReactionWindow;
        w.IsOpen = true; w.SourceSeat = src; w.SourceTileInstanceId = tile.InstanceId;
        w.OpenedAtUtc = DateTime.UtcNow; w.Responses.Clear();

        bool any = false;
        for (int i = 0; i < _gs.PlayerCount; i++)
        {
            if (i == src) continue;
            var avail = _eng.GetAvailableReactions(_gs, _gs.GetPlayer(i), tile);
            if (avail.HasAny)
            {
                any = true;
                RpcId(_seatToPeer[i], nameof(NotifyReactionWindow), S(new ReactionWindowEventDto
                {
                    SourceTile = Dto(tile), SourceSeat = src,
                    TimeoutSeconds = w.TimeoutSeconds,
                    AvailableActions = RpcSerializer.ToDto(avail)
                }));
            }
            else w.SubmitPass(i);
        }
        if (!any) { w.Close(); NextTurn(); }
    }

    private void SubmitReaction(int seat, string actionType, string optionId)
    {
        if (actionType == "pass") { _gs.ReactionWindow.SubmitPass(seat); return; }

        var src = FindSrcTile(); if (src == null) return;
        var p = _gs.GetPlayer(seat);
        var vr = _eng.Validators.ValidateAll(actionType, _gs, p, src);
        if (!vr.IsValid) { Err(seat, vr.Reason); return; }

        var opt = vr.Options.FirstOrDefault(o => o.OptionId == optionId) ?? vr.Options.FirstOrDefault();
        _gs.ReactionWindow.SubmitCandidate(seat, new PlayerActionCandidate
        {
            Seat = seat, ActionType = actionType,
            Priority = actionType switch { "hu" => 100, "gang" => 50, "peng" => 40, "chi" => 10, _ => 30 },
            Params = new() { ["optionId"] = opt?.OptionId ?? "" }
        });
    }

    private void TryResolveWindow()
    {
        var w = _gs.ReactionWindow;
        if (!w.IsOpen) return;
        int src = w.SourceSeat ?? 0;
        if (!w.AllResponded(_gs.PlayerCount, src)) return;

        _gs.Phase = TurnPhase.ResolutionPhase; BroadcastPhase();
        var candidates = w.Responses.Where(kv => kv.Value is { Count: > 0 }).SelectMany(kv => kv.Value!)
            .OrderByDescending(c => c.Priority).ThenBy(c => ((c.Seat - src + _gs.PlayerCount) % _gs.PlayerCount)).ToList();

        // 保存窗口数据后再关闭（DoMeld 需要 srcSeat 和 srcTileId）
        var srcTileId = w.SourceTileInstanceId;
        w.Close();

        if (candidates.Count == 0) { NextTurn(); return; }
        var win = candidates[0];

        if (win.ActionType == "hu") { ServerRoundEnd("ron", win.Seat, src); return; }

        DoMeld(win, src, srcTileId);
        Broadcast(nameof(NotifyReactionResult), new ReactionResultEventDto
        {
            WinnerSeat = win.Seat, ActionType = win.ActionType,
            OptionId = win.Params.GetValueOrDefault("optionId")?.ToString() ?? ""
        });
    }

    private void DoMeld(PlayerActionCandidate c, int srcSeat, Guid? srcTileId)
    {
        var p = _gs.GetPlayer(c.Seat);

        // 从弃牌区找到源牌（用 InstanceId 精确匹配，不用 LastOrDefault）
        MahjongTileState? srcTile = null;
        if (srcSeat >= 0 && srcTileId.HasValue)
        {
            var srcPlayer = _gs.GetPlayer(srcSeat);
            srcTile = srcPlayer.Discards.FirstOrDefault(t => t.InstanceId == srcTileId.Value);
            if (srcTile != null) srcPlayer.Discards.Remove(srcTile);
        }
        if (srcTile == null) { GD.PrintErr($"[RPC] DoMeld 找不到源牌 seat={srcSeat} tile={srcTileId}"); NextTurn(); return; }

        string optId = c.Params.GetValueOrDefault("optionId")?.ToString() ?? "";

        // 重新设置 ReactionWindow 数据以便验证器读取（部分验证器依赖 SourceSeat）
        _gs.ReactionWindow.SourceSeat = srcSeat;
        _gs.ReactionWindow.SourceTileInstanceId = srcTileId;

        var vr = _eng.Validators.ValidateAll(c.ActionType, _gs, p, srcTile);
        var opt = vr.Options.FirstOrDefault(o => o.OptionId == optId) ?? vr.Options.FirstOrDefault();

        // 清除临时恢复的窗口数据
        _gs.ReactionWindow.SourceSeat = null;
        _gs.ReactionWindow.SourceTileInstanceId = null;

        var tiles = new List<MahjongTileState> { srcTile };
        if (opt != null) foreach (var tid in opt.InvolvedTileIds)
        {
            var ht = p.Hand.FirstOrDefault(t => t.InstanceId == tid);
            if (ht != null) { p.Hand.Remove(ht); ht.Zone = MahjongTileZone.Meld; ht.IsLocked = true; tiles.Add(ht); }
        }
        srcTile.Zone = MahjongTileZone.Meld; srcTile.IsLocked = true;

        string kind = c.ActionType switch { "chi" => MeldGroup.KindChi, "peng" => MeldGroup.KindPeng, "gang" => MeldGroup.KindMinkan, _ => c.ActionType };
        p.Melds.Add(new MeldGroup { Kind = kind, Tiles = tiles, SourceSeat = srcSeat, IsOpen = true });
        if (kind != MeldGroup.KindAnkan) p.IsMenzen = false;

        if (c.ActionType == "gang") { RevealDora(); _gs.Phase = TurnPhase.RinShanPhase; _gs.CurrentTurnSeat = c.Seat; BroadcastPhase(); DrawDeadWall(c.Seat); }
        else { _gs.CurrentTurnSeat = c.Seat; _gs.Phase = TurnPhase.DiscardPhase; BroadcastPhase(); BroadcastTurn(c.Seat); }
    }

    private void DoSelfAction(PlayerState p, string type, string optId, ValidationResult vr)
    {
        if (type == "hu") { ServerRoundEnd("tsumo", p.Seat, -1); return; }
        if (type != "gang") return;

        var opt = vr.Options.FirstOrDefault(o => o.OptionId == optId) ?? vr.Options.FirstOrDefault();
        if (opt == null) return;
        string kind = opt.Extra.GetValueOrDefault("kind")?.ToString() ?? MeldGroup.KindAnkan;

        var tiles = new List<MahjongTileState>();
        foreach (var tid in opt.InvolvedTileIds)
        {
            var ht = p.Hand.FirstOrDefault(t => t.InstanceId == tid);
            if (ht != null) { p.Hand.Remove(ht); ht.Zone = MahjongTileZone.Meld; ht.IsLocked = true; tiles.Add(ht); }
        }
        p.Melds.Add(new MeldGroup { Kind = kind, Tiles = tiles, SourceSeat = -1, IsOpen = kind != MeldGroup.KindAnkan });

        Broadcast(nameof(NotifyReactionResult), new ReactionResultEventDto { WinnerSeat = p.Seat, ActionType = "gang", OptionId = opt.OptionId });
        RevealDora(); _gs.Phase = TurnPhase.RinShanPhase; BroadcastPhase(); DrawDeadWall(p.Seat);
    }

    private void DrawDeadWall(int seat)
    {
        if (_gs.DeadWall.Count == 0) { ServerRoundEnd("draw", null, -1); return; }
        var p = _gs.GetPlayer(seat);
        var ts = _gs.DeadWall[0]; _gs.DeadWall.RemoveAt(0);
        ts.Zone = MahjongTileZone.Hand; ts.OwnerSeat = seat; p.Hand.Add(ts);

        RpcId(_seatToPeer[seat], nameof(NotifyDraw), S(new DrawEventDto { Seat = seat, Tile = Dto(ts, true), WallRemaining = _gs.Wall.Count, IsRinshan = true }));
        string h = S(new DrawEventDto { Seat = seat, Tile = Dto(ts, false), WallRemaining = _gs.Wall.Count, IsRinshan = true });
        foreach (var kv in _seatToPeer.Where(kv => kv.Key != seat)) RpcId(kv.Value, nameof(NotifyDraw), h);

        _gs.Phase = TurnPhase.SelfActionPhase; BroadcastPhase();
        var sa = _eng.GetAvailableSelfActions(_gs, p, ts);
        if (sa.HasAny) RpcId(_seatToPeer[seat], nameof(NotifySelfActions), S(new SelfActionsEventDto { AvailableActions = RpcSerializer.ToDto(sa) }));
    }

    private void RevealDora()
    {
        if (_gs.RevealedTiles.Count >= 5 || _gs.DeadWall.Count == 0) return;
        var ind = _gs.DeadWall.Last(); _gs.DeadWall.Remove(ind);
        ind.Zone = MahjongTileZone.Reveal; ind.IsFaceUp = true; _gs.RevealedTiles.Add(ind);
        Broadcast(nameof(NotifyDoraReveal), new DoraRevealEventDto { NewIndicator = Dto(ind), TotalDoraCount = _gs.RevealedTiles.Count });
    }

    private void NextTurn()
    {
        foreach (var p in _gs.Players) p.IsIppatsu = false;
        int next = _gs.NextSeat(_gs.CurrentTurnSeat);
        _gs.CurrentTurnSeat = next; _gs.TurnCount++;
        _gs.GetPlayer(next).HasDrawnThisTurn = false;
        _gs.Phase = TurnPhase.DrawPhase; BroadcastPhase(); BroadcastTurn(next);
        ServerDraw(next);
    }

    private void ServerRoundEnd(string reason, int? winner, int loser)
    {
        _gs.Phase = TurnPhase.RoundEnd; _gs.IsRoundEnded = true; BroadcastPhase();

        var dto = new RoundEndEventDto { Reason = reason, WinnerSeat = winner, LoserSeat = loser >= 0 ? loser : null };
        if (winner.HasValue)
        {
            var w = _gs.GetPlayer(winner.Value);
            dto.WinnerHand = w.Hand.Select(t => Dto(t)).ToList();
            int han = w.Counters.GetValueOrDefault("last_win_han", 0);
            int fu = w.Counters.GetValueOrDefault("last_win_fu", 30);
            bool yakuman = w.RuntimeTags.Contains("last_win_yakuman");
            if (han > 0)
            {
                var sc = new ScoreCalculator();
                dto.TotalHan = han; dto.Fu = fu; dto.IsYakuman = yakuman;
                dto.ScoreChanges = sc.CalculateScoreChanges(_gs, winner.Value, loser >= 0 ? loser : null, han, fu, yakuman, reason == "tsumo");
                foreach (var (s, ch) in dto.ScoreChanges) _gs.GetPlayer(s).Score += ch;
            }
        }
        Broadcast(nameof(NotifyRoundEnd), dto);
    }

    public void CheckReactionTimeout()
    {
        var w = _gs.ReactionWindow;
        if (!w.IsOpen || w.OpenedAtUtc == null) return;
        if ((DateTime.UtcNow - w.OpenedAtUtc.Value).TotalSeconds < w.TimeoutSeconds) return;
        int src = w.SourceSeat ?? 0;
        for (int i = 0; i < _gs.PlayerCount; i++) { if (i == src) continue; if (!w.Responses.ContainsKey(i) || w.Responses[i] == null) w.SubmitPass(i); }
        TryResolveWindow();
    }

    // ═══ 辅助 ═══

    private void Broadcast<T>(string method, T dto) { string j = S(dto); Rpc(method, j); }
    private void BroadcastPhase() { Broadcast(nameof(NotifyPhaseChange), new PhaseChangeEventDto { Phase = _gs.Phase.ToString(), CurrentSeat = _gs.CurrentTurnSeat }); }
    private void BroadcastTurn(int seat) { Broadcast(nameof(NotifyTurnStart), new TurnStartEventDto { Seat = seat, TurnCount = _gs.TurnCount }); }
    private void Err(int seat, string msg) { if (_seatToPeer.TryGetValue(seat, out long pid)) RpcId(pid, nameof(NotifyError), msg); }
    private int GetSeatByPeer(long pid)
    {
        // GetRemoteSenderId() 返回 0 表示本地调用（房主/单人）→ 映射为自身 peerId
        if (pid == 0) pid = Multiplayer.GetUniqueId();
        foreach (var kv in _seatToPeer) if (kv.Value == pid) return kv.Key;
        return -1;
    }
    private MahjongTileState? FindSrcTile() { var id = _gs.ReactionWindow.SourceTileInstanceId; return id == null ? null : _eng.FindTileById(_gs, id.Value); }
    private static string S<T>(T o) => RpcSerializer.Serialize(o);
    private static T? D<T>(string j) => RpcSerializer.Deserialize<T>(j);
    private static TileDto Dto(MahjongTileState t, bool reveal = true) => RpcSerializer.ToDto(t, reveal);

    private GameInitEventDto BuildInitDto(int forSeat)
    {
        var dto = new GameInitEventDto
        {
            MySeat = forSeat, DealerSeat = _gs.DealerSeat, RoundWind = _gs.RoundWind,
            Honba = _gs.Honba, RiichiSticks = _gs.RiichiSticks, WallRemaining = _gs.Wall.Count,
            DoraIndicators = _gs.RevealedTiles.Select(t => Dto(t)).ToList()
        };
        foreach (var p in _gs.Players)
        {
            var info = new PlayerInfoDto
            {
                Seat = p.Seat, Score = p.Score, SeatWind = p.SeatWind,
                IsRiichi = p.IsRiichi, HandCount = p.Hand.Count,
                Melds = p.Melds.Select(RpcSerializer.ToDto).ToList(),
                Discards = p.Discards.Select(t => Dto(t)).ToList()
            };
            if (p.Seat == forSeat) info.HandTiles = p.Hand.Select(t => Dto(t, true)).ToList();
            dto.Players.Add(info);
        }
        return dto;
    }
}