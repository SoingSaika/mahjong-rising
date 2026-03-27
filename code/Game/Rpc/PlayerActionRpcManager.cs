using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;
using MahjongRising.code.Yaku;

namespace MahjongRising.code.Game.Rpc;

public partial class PlayerActionRpcManager : Node
{
    private MahjongGameState _gs = null!;
    private MahjongRuleEngine _eng = null!;
    private YakuRuleRegistry _yakuRules = null!;
    private ScoreCalculator _scoring = null!;
    private readonly Dictionary<int, long> _seatToPeer = new();
    private readonly HashSet<long> _realPeers = new();

    public event Action<GameInitEventDto>? OnGameInit;
    public event Action<DrawEventDto>? OnDraw;
    public event Action<DiscardEventDto>? OnDiscard;
    public event Action<RiichiEventDto>? OnRiichi;
    public event Action<ReactionWindowEventDto>? OnReactionWindow;
    public event Action<ReactionResultEventDto>? OnReactionResult;
    public event Action<DoraRevealEventDto>? OnDoraReveal;
    public event Action<TurnStartEventDto>? OnTurnStart;
    public event Action<SelfActionsEventDto>? OnSelfActions;
    public event Action<RoundEndEventDto>? OnRoundEnd;
    public event Action<string>? OnError;

    public void Initialize(MahjongGameState gs, MahjongRuleEngine eng, YakuRuleRegistry yakuRules, ScoreCalculator scoring)
    {
        _gs = gs; _eng = eng; _yakuRules = yakuRules; _scoring = scoring;
        _seatToPeer.Clear(); _realPeers.Clear();
        foreach (var p in _gs.Players)
        {
            _seatToPeer[p.Seat] = p.PeerId;
            if (p.PeerId > 0 && p.PeerId < 100000) _realPeers.Add(p.PeerId);
        }
    }

    private bool IsReal(int seat) => _seatToPeer.TryGetValue(seat, out long pid) && _realPeers.Contains(pid);
    private void SendTo(int seat, string method, string json) { if (IsReal(seat)) RpcId(_seatToPeer[seat], method, json); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDiscard(string json) { if (!Multiplayer.IsServer()) return; int seat = Peer(); if (seat < 0) return; var req = D<DiscardRequestDto>(json); if (req == null) return; if (!_eng.IsCurrentPlayer(_gs, seat)) return; if (_gs.Phase != TurnPhase.DiscardPhase && _gs.Phase != TurnPhase.SelfActionPhase) return; var p = _gs.GetPlayer(seat); if (!Guid.TryParse(req.TileInstanceId, out var id)) return; var ts = _eng.FindTileInHand(p, id); if (ts == null || ts.IsLocked) return; DoDiscard(p, ts, req.IsRiichi); }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestReaction(string json) { if (!Multiplayer.IsServer()) return; int seat = Peer(); if (seat < 0) return; var req = D<ReactionRequestDto>(json); if (req == null) return; if (_gs.Phase != TurnPhase.ReactionPhase || !_gs.ReactionWindow.IsOpen) return; if (seat == _gs.ReactionWindow.SourceSeat) return; SubmitReaction(seat, req.ActionType, req.OptionId); TryResolveWindow(); }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSelfAction(string json) { if (!Multiplayer.IsServer()) return; int seat = Peer(); if (seat < 0) return; var req = D<SelfActionRequestDto>(json); if (req == null) return; if (!_eng.IsCurrentPlayer(_gs, seat) || _gs.Phase != TurnPhase.SelfActionPhase) return; var p = _gs.GetPlayer(seat); var last = p.Hand.LastOrDefault(); if (last == null) return; var vr = _eng.Validators.ValidateAll(req.ActionType, _gs, p, last); if (!vr.IsValid) return; DoSelfAction(p, req.ActionType, req.OptionId, vr); }

    public void HandleAiDiscard(int seat, string json) { var req = D<DiscardRequestDto>(json); if (req == null) return; if (!_eng.IsCurrentPlayer(_gs, seat)) return; if (_gs.Phase != TurnPhase.DiscardPhase && _gs.Phase != TurnPhase.SelfActionPhase) return; var p = _gs.GetPlayer(seat); if (!Guid.TryParse(req.TileInstanceId, out var id)) return; var ts = _eng.FindTileInHand(p, id); if (ts == null || ts.IsLocked) return; DoDiscard(p, ts, req.IsRiichi); }
    public void HandleAiSelfAction(int seat, string json) { var req = D<SelfActionRequestDto>(json); if (req == null) return; if (!_eng.IsCurrentPlayer(_gs, seat) || _gs.Phase != TurnPhase.SelfActionPhase) return; var p = _gs.GetPlayer(seat); var last = p.Hand.LastOrDefault(); if (last == null) return; var vr = _eng.Validators.ValidateAll(req.ActionType, _gs, p, last); if (!vr.IsValid) return; DoSelfAction(p, req.ActionType, req.OptionId, vr); }
    public void HandleAiReaction(int seat, string json) { var req = D<ReactionRequestDto>(json); if (req == null) return; if (_gs.Phase != TurnPhase.ReactionPhase || !_gs.ReactionWindow.IsOpen) return; if (seat == _gs.ReactionWindow.SourceSeat) return; SubmitReaction(seat, req.ActionType, req.OptionId); TryResolveWindow(); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyGameInit(string j) { OnGameInit?.Invoke(D<GameInitEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyDraw(string j) { OnDraw?.Invoke(D<DrawEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyDiscard(string j) { OnDiscard?.Invoke(D<DiscardEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyRiichi(string j) { OnRiichi?.Invoke(D<RiichiEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyReactionWindow(string j) { OnReactionWindow?.Invoke(D<ReactionWindowEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyReactionResult(string j) { OnReactionResult?.Invoke(D<ReactionResultEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyDoraReveal(string j) { OnDoraReveal?.Invoke(D<DoraRevealEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyTurnStart(string j) { OnTurnStart?.Invoke(D<TurnStartEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifySelfActions(string j) { OnSelfActions?.Invoke(D<SelfActionsEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyRoundEnd(string j) { OnRoundEnd?.Invoke(D<RoundEndEventDto>(j)!); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void NotifyError(string j) { OnError?.Invoke(j); }

    public void ServerStartRound()
    {
        _gs.Phase = TurnPhase.RoundStart;
        for (int i = 0; i < _gs.PlayerCount; i++) SendTo(i, nameof(NotifyGameInit), S(BuildInitDto(i)));
        _gs.CurrentTurnSeat = _gs.DealerSeat; _gs.Phase = TurnPhase.DrawPhase;
        ServerDraw(_gs.DealerSeat);
    }

    public void ServerDraw(int seat)
    {
        if (_gs.Wall.Count == 0) { ServerRoundEnd("draw", null, -1); return; }
        var p = _gs.GetPlayer(seat); var ts = _gs.Wall[0]; _gs.Wall.RemoveAt(0);
        ts.Zone = MahjongTileZone.Hand; ts.OwnerSeat = seat; p.Hand.Add(ts); p.HasDrawnThisTurn = true;

        SendTo(seat, nameof(NotifyDraw), S(new DrawEventDto { Seat = seat, Tile = Dto(ts, true), WallRemaining = _gs.Wall.Count }));
        string h = S(new DrawEventDto { Seat = seat, Tile = Dto(ts, false), WallRemaining = _gs.Wall.Count });
        foreach (var kv in _seatToPeer.Where(kv => kv.Key != seat)) if (_realPeers.Contains(kv.Value)) RpcId(kv.Value, nameof(NotifyDraw), h);

        _ = _eng.TriggerDrawAction(_gs, p, ts);
        _gs.Phase = TurnPhase.SelfActionPhase;
        var sa = _eng.GetAvailableSelfActions(_gs, p, ts);
        if (sa.HasAny) SendTo(seat, nameof(NotifySelfActions), S(new SelfActionsEventDto { AvailableActions = RpcSerializer.ToDto(sa) }));
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
        _gs.Phase = TurnPhase.ReactionPhase;
        var w = _gs.ReactionWindow; w.IsOpen = true; w.SourceSeat = src; w.SourceTileInstanceId = tile.InstanceId; w.OpenedAtUtc = DateTime.UtcNow; w.Responses.Clear();
        bool any = false;
        for (int i = 0; i < _gs.PlayerCount; i++)
        {
            if (i == src) continue;
            var avail = _eng.GetAvailableReactions(_gs, _gs.GetPlayer(i), tile);
            if (avail.HasAny) { any = true; SendTo(i, nameof(NotifyReactionWindow), S(new ReactionWindowEventDto { SourceTile = Dto(tile), SourceSeat = src, TimeoutSeconds = w.TimeoutSeconds, AvailableActions = RpcSerializer.ToDto(avail) })); }
            else w.SubmitPass(i);
        }
        if (!any) { w.Close(); NextTurn(); }
    }

    private void SubmitReaction(int seat, string actionType, string optionId)
    {
        if (actionType == "pass") { _gs.ReactionWindow.SubmitPass(seat); return; }
        var src = FindSrcTile(); if (src == null) return;
        var p = _gs.GetPlayer(seat); var vr = _eng.Validators.ValidateAll(actionType, _gs, p, src);
        if (!vr.IsValid) { Err(seat, vr.Reason); return; }
        var opt = vr.Options.FirstOrDefault(o => o.OptionId == optionId) ?? vr.Options.FirstOrDefault();
        _gs.ReactionWindow.SubmitCandidate(seat, new PlayerActionCandidate { Seat = seat, ActionType = actionType, Priority = actionType switch { "hu" => 100, "gang" => 50, "peng" => 40, "chi" => 10, _ => 30 }, Params = new() { ["optionId"] = opt?.OptionId ?? "" } });
    }

    private void TryResolveWindow()
    {
        var w = _gs.ReactionWindow; if (!w.IsOpen) return;
        int src = w.SourceSeat ?? 0; if (!w.AllResponded(_gs.PlayerCount, src)) return;
        _gs.Phase = TurnPhase.ResolutionPhase;
        var cands = w.Responses.Where(kv => kv.Value is { Count: > 0 }).SelectMany(kv => kv.Value!).OrderByDescending(c => c.Priority).ThenBy(c => ((c.Seat - src + _gs.PlayerCount) % _gs.PlayerCount)).ToList();
        var srcTileId = w.SourceTileInstanceId; w.Close();
        if (cands.Count == 0) { NextTurn(); return; }
        var win = cands[0];
        if (win.ActionType == "hu") { ServerRoundEnd("ron", win.Seat, src); return; }
        var meldResult = DoMeld(win, src, srcTileId);
        Broadcast(nameof(NotifyReactionResult), meldResult);
    }

    private ReactionResultEventDto DoMeld(PlayerActionCandidate c, int srcSeat, Guid? srcTileId)
    {
        var p = _gs.GetPlayer(c.Seat);
        var result = new ReactionResultEventDto { WinnerSeat = c.Seat, ActionType = c.ActionType, OptionId = c.Params.GetValueOrDefault("optionId")?.ToString() ?? "" };
        MahjongTileState? srcTile = null;
        if (srcSeat >= 0 && srcTileId.HasValue) { var sp = _gs.GetPlayer(srcSeat); srcTile = sp.Discards.FirstOrDefault(t => t.InstanceId == srcTileId.Value); if (srcTile != null) sp.Discards.Remove(srcTile); }
        if (srcTile == null) { GD.PrintErr("[RPC] DoMeld no src tile"); NextTurn(); return result; }

        _gs.ReactionWindow.SourceSeat = srcSeat; _gs.ReactionWindow.SourceTileInstanceId = srcTileId;
        var vr = _eng.Validators.ValidateAll(c.ActionType, _gs, p, srcTile);
        var opt = vr.Options.FirstOrDefault(o => o.OptionId == result.OptionId) ?? vr.Options.FirstOrDefault();
        _gs.ReactionWindow.SourceSeat = null; _gs.ReactionWindow.SourceTileInstanceId = null;

        var meldTiles = new List<MahjongTileState> { srcTile }; var removedIds = new List<string>();
        if (opt != null) foreach (var tid in opt.InvolvedTileIds) { var ht = p.Hand.FirstOrDefault(t => t.InstanceId == tid); if (ht != null) { p.Hand.Remove(ht); ht.Zone = MahjongTileZone.Meld; ht.IsLocked = true; meldTiles.Add(ht); removedIds.Add(ht.InstanceId.ToString()); } }
        srcTile.Zone = MahjongTileZone.Meld; srcTile.IsLocked = true;
        string kind = c.ActionType switch { "chi" => MeldGroup.KindChi, "peng" => MeldGroup.KindPeng, "gang" => MeldGroup.KindMinkan, _ => c.ActionType };
        var mg = new MeldGroup { Kind = kind, Tiles = meldTiles, SourceSeat = srcSeat, IsOpen = true }; p.Melds.Add(mg);
        if (kind != MeldGroup.KindAnkan) p.IsMenzen = false;
        result.NewMeld = RpcSerializer.ToDto(mg); result.RemovedTileIds = removedIds; result.SourceSeat = srcSeat; result.SourceTileId = srcTile.InstanceId.ToString();

        if (c.ActionType == "gang") { RevealDora(); _gs.Phase = TurnPhase.RinShanPhase; _gs.CurrentTurnSeat = c.Seat; DrawDeadWall(c.Seat); }
        else { _gs.CurrentTurnSeat = c.Seat; _gs.Phase = TurnPhase.DiscardPhase; BroadcastTurn(c.Seat); }
        return result;
    }

    private void DoSelfAction(PlayerState p, string type, string optId, ValidationResult vr)
    {
        if (type == "hu") { ServerRoundEnd("tsumo", p.Seat, -1); return; }
        if (type != "gang") return;
        var opt = vr.Options.FirstOrDefault(o => o.OptionId == optId) ?? vr.Options.FirstOrDefault(); if (opt == null) return;
        string kind = opt.Extra.GetValueOrDefault("kind")?.ToString() ?? MeldGroup.KindAnkan;
        var meldTiles = new List<MahjongTileState>(); var removedIds = new List<string>();
        foreach (var tid in opt.InvolvedTileIds) { var ht = p.Hand.FirstOrDefault(t => t.InstanceId == tid); if (ht != null) { p.Hand.Remove(ht); ht.Zone = MahjongTileZone.Meld; ht.IsLocked = true; meldTiles.Add(ht); removedIds.Add(ht.InstanceId.ToString()); } }
        var mg = new MeldGroup { Kind = kind, Tiles = meldTiles, SourceSeat = -1, IsOpen = kind != MeldGroup.KindAnkan }; p.Melds.Add(mg);
        Broadcast(nameof(NotifyReactionResult), new ReactionResultEventDto { WinnerSeat = p.Seat, ActionType = "gang", OptionId = opt.OptionId, NewMeld = RpcSerializer.ToDto(mg), RemovedTileIds = removedIds });
        RevealDora(); _gs.Phase = TurnPhase.RinShanPhase; DrawDeadWall(p.Seat);
    }

    private void DrawDeadWall(int seat)
    {
        if (_gs.DeadWall.Count == 0) { ServerRoundEnd("draw", null, -1); return; }
        var p = _gs.GetPlayer(seat); var ts = _gs.DeadWall[0]; _gs.DeadWall.RemoveAt(0);
        ts.Zone = MahjongTileZone.Hand; ts.OwnerSeat = seat; p.Hand.Add(ts);
        SendTo(seat, nameof(NotifyDraw), S(new DrawEventDto { Seat = seat, Tile = Dto(ts, true), WallRemaining = _gs.Wall.Count, IsRinshan = true }));
        string h = S(new DrawEventDto { Seat = seat, Tile = Dto(ts, false), WallRemaining = _gs.Wall.Count, IsRinshan = true });
        foreach (var kv in _seatToPeer.Where(kv => kv.Key != seat)) if (_realPeers.Contains(kv.Value)) RpcId(kv.Value, nameof(NotifyDraw), h);
        _gs.Phase = TurnPhase.SelfActionPhase;
        var sa = _eng.GetAvailableSelfActions(_gs, p, ts);
        if (sa.HasAny) SendTo(seat, nameof(NotifySelfActions), S(new SelfActionsEventDto { AvailableActions = RpcSerializer.ToDto(sa) }));
    }

    private void RevealDora() { if (_gs.RevealedTiles.Count >= 5 || _gs.DeadWall.Count == 0) return; var ind = _gs.DeadWall.Last(); _gs.DeadWall.Remove(ind); ind.Zone = MahjongTileZone.Reveal; ind.IsFaceUp = true; _gs.RevealedTiles.Add(ind); Broadcast(nameof(NotifyDoraReveal), new DoraRevealEventDto { NewIndicator = Dto(ind), TotalDoraCount = _gs.RevealedTiles.Count }); }

    private void NextTurn() { foreach (var p in _gs.Players) p.IsIppatsu = false; int next = _gs.NextSeat(_gs.CurrentTurnSeat); _gs.CurrentTurnSeat = next; _gs.TurnCount++; _gs.GetPlayer(next).HasDrawnThisTurn = false; _gs.Phase = TurnPhase.DrawPhase; BroadcastTurn(next); ServerDraw(next); }

    private void ServerRoundEnd(string reason, int? winner, int loser)
    {
        _gs.Phase = TurnPhase.RoundEnd; _gs.IsRoundEnded = true;
        var dto = new RoundEndEventDto { Reason = reason, WinnerSeat = winner, LoserSeat = loser >= 0 ? loser : null };
        if (winner.HasValue && reason is "tsumo" or "ron")
        {
            var w = _gs.GetPlayer(winner.Value);
            dto.WinnerHand = w.Hand.Select(t => Dto(t)).ToList();
            MahjongTileState? winTile = reason == "tsumo" ? w.Hand.LastOrDefault() : (loser >= 0 ? _gs.GetPlayer(loser).Discards.LastOrDefault() : null);
            if (winTile != null)
            {
                var tempHand = w.Hand.Select(t => t.Tile).ToList();
                if (reason == "ron") tempHand.Add(winTile.Tile);
                var evalCtx = YakuEvalContext.Create(_gs, w, winTile, tempHand);
                var yakuList = _yakuRules.EvaluateAll(evalCtx);
                if (yakuList.Count > 0)
                {
                    int totalHan = yakuList.Sum(y => y.Han); bool isYakuman = yakuList.Any(y => y.IsYakuman);
                    int fu = _scoring.CalculateFu(evalCtx, yakuList);
                    dto.YakuIds = yakuList.Select(y => y.YakuId).ToList();
                    dto.YakuNames = yakuList.Select(y => { var def = _yakuRules.GetDefinition(y.YakuId); return def != null ? $"{def.NameJp}({y.Han}番)" : $"{y.YakuId}({y.Han}番)"; }).ToList();
                    dto.TotalHan = totalHan; dto.Fu = fu; dto.IsYakuman = isYakuman;
                    dto.BasePoints = _scoring.CalculateBasePoints(totalHan, fu, isYakuman);
                    dto.ScoreChanges = _scoring.CalculateScoreChanges(_gs, winner.Value, loser >= 0 ? loser : null, totalHan, fu, isYakuman, reason == "tsumo");
                    foreach (var (s, ch) in dto.ScoreChanges) _gs.GetPlayer(s).Score += ch;
                }
            }
        }
        Broadcast(nameof(NotifyRoundEnd), dto);
    }

    public void CheckReactionTimeout() { var w = _gs.ReactionWindow; if (!w.IsOpen || w.OpenedAtUtc == null) return; if ((DateTime.UtcNow - w.OpenedAtUtc.Value).TotalSeconds < w.TimeoutSeconds) return; int src = w.SourceSeat ?? 0; for (int i = 0; i < _gs.PlayerCount; i++) if (i != src && (!w.Responses.ContainsKey(i) || w.Responses[i] == null)) w.SubmitPass(i); TryResolveWindow(); }

    private void Broadcast<T>(string method, T dto) { Rpc(method, S(dto)); }
    private void BroadcastTurn(int seat) { Broadcast(nameof(NotifyTurnStart), new TurnStartEventDto { Seat = seat, TurnCount = _gs.TurnCount, Phase = _gs.Phase.ToString() }); }
    private void Err(int seat, string msg) { SendTo(seat, nameof(NotifyError), msg); }
    private int Peer() { long pid = Multiplayer.GetRemoteSenderId(); if (pid == 0) pid = Multiplayer.GetUniqueId(); foreach (var kv in _seatToPeer) if (kv.Value == pid) return kv.Key; return -1; }
    private MahjongTileState? FindSrcTile() { var id = _gs.ReactionWindow.SourceTileInstanceId; return id == null ? null : _eng.FindTileById(_gs, id.Value); }
    private static string S<T>(T o) => RpcSerializer.Serialize(o);
    private static T? D<T>(string j) => RpcSerializer.Deserialize<T>(j);
    private static TileDto Dto(MahjongTileState t, bool reveal = true) => RpcSerializer.ToDto(t, reveal);

    private GameInitEventDto BuildInitDto(int forSeat)
    {
        var dto = new GameInitEventDto { MySeat = forSeat, DealerSeat = _gs.DealerSeat, RoundWind = _gs.RoundWind, Honba = _gs.Honba, RiichiSticks = _gs.RiichiSticks, WallRemaining = _gs.Wall.Count, DoraIndicators = _gs.RevealedTiles.Select(t => Dto(t)).ToList() };
        foreach (var p in _gs.Players)
        {
            var info = new PlayerInfoDto { Seat = p.Seat, Score = p.Score, SeatWind = p.SeatWind, IsRiichi = p.IsRiichi, HandCount = p.Hand.Count, Melds = p.Melds.Select(RpcSerializer.ToDto).ToList(), Discards = p.Discards.Select(t => Dto(t)).ToList() };
            if (p.Seat == forSeat) info.HandTiles = p.Hand.Select(t => Dto(t, true)).ToList();
            dto.Players.Add(info);
        }
        return dto;
    }
}