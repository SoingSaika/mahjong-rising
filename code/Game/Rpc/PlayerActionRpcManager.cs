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

/// <summary>
/// Godot RPC 管理器（完整版）。
/// 服务器权威模式，客户端只发请求、收结果。
///
/// RPC 清单（12 Server→Client + 5 Client→Server）：
///
/// Server → Client:
///   NotifyGameInit         开局初始化（配牌、宝牌、座位）     定向  ~520B/人
///   NotifyDraw             摸牌                              定向  ~50B
///   NotifyDiscard          弃牌                              广播  ~60B
///   NotifyRiichi           立直宣告确认                       广播  ~30B
///   NotifyReactionWindow   反应窗口打开                       定向  ~200B
///   NotifyReactionResult   反应结算                           广播  ~80B
///   NotifyDoraReveal       新宝牌翻开                         广播  ~40B
///   NotifyTurnStart        回合开始                           广播  ~20B
///   NotifyPhaseChange      阶段变更                           广播  ~15B
///   NotifySelfActions      自摸可用动作                       定向  ~150B
///   NotifyScoreUpdate      分数变动                           广播  ~40B
///   NotifyRoundEnd         局结束                             广播  ~300B
///   NotifyFullSync         断线重连全量同步                   定向  ~2KB
///   NotifyError            错误信息                           定向  ~50B
///
/// Client → Server:
///   RequestDiscard         请求弃牌
///   RequestReaction        提交反应
///   RequestSelfAction      自摸动作
///   RequestCharacterSelect 选择角色
///   RequestReady           准备就绪
///   RequestResync          请求重连同步
/// </summary>
public partial class PlayerActionRpcManager : Node
{
    private MahjongGameState _gameState = null!;
    private MahjongRuleEngine _engine = null!;
    private readonly Dictionary<int, long> _seatToPeer = new();
    private readonly Dictionary<int, string> _seatToCharacter = new();
    private readonly HashSet<int> _readySeats = new();

    public void Initialize(MahjongGameState gameState, MahjongRuleEngine engine)
    {
        _gameState = gameState;
        _engine = engine;
        foreach (var player in _gameState.Players)
            _seatToPeer[player.Seat] = player.PeerId;
    }

    // ═══════════════════════════════════════════
    // Client → Server
    // ═══════════════════════════════════════════

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestCharacterSelect(string json)
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        int seat = GetSeatByPeer(senderId);
        if (seat < 0) return;

        var req = RpcSerializer.Deserialize<CharacterSelectRequestDto>(json);
        if (req == null) return;

        _seatToCharacter[seat] = req.CharacterId;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestReady(string json)
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        int seat = GetSeatByPeer(senderId);
        if (seat < 0) return;

        var req = RpcSerializer.Deserialize<ReadyRequestDto>(json);
        if (req == null) return;

        if (req.IsReady) _readySeats.Add(seat); else _readySeats.Remove(seat);

        // 所有人准备好后可以开局
        if (_readySeats.Count == _gameState.PlayerCount)
            ServerStartRound();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDiscard(string json)
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        int seat = GetSeatByPeer(senderId);
        if (seat < 0) return;

        var request = RpcSerializer.Deserialize<DiscardRequestDto>(json);
        if (request == null) return;

        if (!_engine.IsCurrentPlayer(_gameState, seat))
        { SendError(senderId, "不是你的回合"); return; }
        if (_gameState.Phase != TurnPhase.DiscardPhase && _gameState.Phase != TurnPhase.SelfActionPhase)
        { SendError(senderId, "当前阶段不能弃牌"); return; }

        var player = _gameState.GetPlayer(seat);
        if (!Guid.TryParse(request.TileInstanceId, out var tileId))
        { SendError(senderId, "无效的牌 ID"); return; }

        var tileState = _engine.FindTileInHand(player, tileId);
        if (tileState == null) { SendError(senderId, "手牌中没有这张牌"); return; }
        if (tileState.IsLocked) { SendError(senderId, "这张牌被锁定"); return; }

        ExecuteDiscard(player, tileState, request.IsRiichi);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestReaction(string json)
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        int seat = GetSeatByPeer(senderId);
        if (seat < 0) return;

        var request = RpcSerializer.Deserialize<ReactionRequestDto>(json);
        if (request == null) return;

        if (_gameState.Phase != TurnPhase.ReactionPhase || !_gameState.ReactionWindow.IsOpen)
        { SendError(senderId, "当前没有反应窗口"); return; }
        if (seat == _gameState.ReactionWindow.SourceSeat)
        { SendError(senderId, "不能对自己弃的牌做反应"); return; }

        if (request.ActionType == "pass")
        {
            _gameState.ReactionWindow.SubmitPass(seat);
        }
        else
        {
            var sourceTile = FindSourceTile();
            if (sourceTile == null) return;

            var player = _gameState.GetPlayer(seat);
            var validResult = _engine.Validators.ValidateAll(
                request.ActionType, _gameState, player, sourceTile);

            if (!validResult.IsValid)
            { SendError(senderId, $"动作不合法：{validResult.Reason}"); return; }

            var option = validResult.Options.FirstOrDefault(o => o.OptionId == request.OptionId)
                ?? validResult.Options.FirstOrDefault();

            _gameState.ReactionWindow.SubmitCandidate(seat, new PlayerActionCandidate
            {
                Seat = seat,
                ActionType = request.ActionType,
                Priority = GetActionPriority(request.ActionType),
                Params = new Dictionary<string, object> { ["optionId"] = option?.OptionId ?? "" }
            });
        }

        TryResolveReactionWindow();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSelfAction(string json)
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        int seat = GetSeatByPeer(senderId);
        if (seat < 0) return;

        var request = RpcSerializer.Deserialize<SelfActionRequestDto>(json);
        if (request == null) return;

        if (!_engine.IsCurrentPlayer(_gameState, seat) || _gameState.Phase != TurnPhase.SelfActionPhase)
        { SendError(senderId, "当前不是自摸动作阶段"); return; }

        var player = _gameState.GetPlayer(seat);
        var lastDrawn = player.Hand.LastOrDefault();
        if (lastDrawn == null) return;

        var validResult = _engine.Validators.ValidateAll(
            request.ActionType, _gameState, player, lastDrawn);
        if (!validResult.IsValid)
        { SendError(senderId, $"动作不合法：{validResult.Reason}"); return; }

        ExecuteSelfAction(player, request.ActionType, request.OptionId, validResult);
    }

    /// <summary>客户端请求断线重连同步。</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestResync()
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        int seat = GetSeatByPeer(senderId);
        if (seat < 0) return;

        SendFullSync(seat);
    }

    // ═══════════════════════════════════════════
    // Server → Client（Godot [Rpc] 声明）
    // ═══════════════════════════════════════════

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyGameInit(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyDraw(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyDiscard(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyRiichi(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyReactionWindow(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyReactionResult(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyDoraReveal(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyTurnStart(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyPhaseChange(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifySelfActions(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyScoreUpdate(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyRoundEnd(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyFullSync(string json) { }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyError(string message) { }

    // ═══════════════════════════════════════════
    // 服务器内部逻辑
    // ═══════════════════════════════════════════

    /// <summary>开局：配牌、翻宝牌、广播初始状态。</summary>
    public void ServerStartRound()
    {
        _gameState.Phase = TurnPhase.RoundStart;
        BroadcastPhaseChange();

        // 为每位玩家生成初始化 DTO（手牌只发给自己）
        for (int i = 0; i < _gameState.PlayerCount; i++)
        {
            var dto = BuildGameInitDto(i);
            RpcId(_seatToPeer[i], nameof(NotifyGameInit), RpcSerializer.Serialize(dto));
        }

        // 庄家先手
        _gameState.CurrentTurnSeat = _gameState.DealerSeat;
        _gameState.Phase = TurnPhase.DrawPhase;
        BroadcastPhaseChange();
        ServerDraw(_gameState.DealerSeat);
    }

    /// <summary>摸牌。</summary>
    public void ServerDraw(int seat)
    {
        if (_gameState.Wall.Count == 0) { ServerRoundEnd("draw", null, -1); return; }

        var player = _gameState.GetPlayer(seat);
        var tileState = _gameState.Wall[0];
        _gameState.Wall.RemoveAt(0);

        tileState.Zone = MahjongTileZone.Hand;
        tileState.OwnerSeat = seat;
        player.Hand.Add(tileState);
        player.HasDrawnThisTurn = true;

        // 通知摸牌者（明牌）
        RpcId(_seatToPeer[seat], nameof(NotifyDraw), RpcSerializer.Serialize(new DrawEventDto
        {
            Seat = seat,
            Tile = RpcSerializer.ToDto(tileState, reveal: true),
            WallRemaining = _gameState.Wall.Count
        }));

        // 通知其他人（暗牌）
        string hiddenJson = RpcSerializer.Serialize(new DrawEventDto
        {
            Seat = seat,
            Tile = RpcSerializer.ToDto(tileState, reveal: false),
            WallRemaining = _gameState.Wall.Count
        });
        foreach (var kv in _seatToPeer.Where(kv => kv.Key != seat))
            RpcId(kv.Value, nameof(NotifyDraw), hiddenJson);

        _ = _engine.TriggerDrawAction(_gameState, player, tileState);

        _gameState.Phase = TurnPhase.SelfActionPhase;
        BroadcastPhaseChange();

        // 检查自摸可用动作
        var selfActions = _engine.GetAvailableSelfActions(_gameState, player, tileState);
        if (selfActions.HasAny)
        {
            RpcId(_seatToPeer[seat], nameof(NotifySelfActions),
                RpcSerializer.Serialize(new SelfActionsEventDto
                {
                    AvailableActions = RpcSerializer.ToDto(selfActions)
                }));
        }
    }

    /// <summary>弃牌。</summary>
    private void ExecuteDiscard(PlayerState player, MahjongTileState tileState, bool isRiichi)
    {
        bool isTsumogiri = player.Hand.Count > 0 && player.Hand[^1].InstanceId == tileState.InstanceId;

        player.Hand.Remove(tileState);
        tileState.Zone = MahjongTileZone.Discard;
        tileState.IsFaceUp = true;
        player.Discards.Add(tileState);

        // 立直处理
        if (isRiichi && player.IsMenzen && !player.IsRiichi)
        {
            player.IsRiichi = true;
            player.IsIppatsu = true;
            player.Score -= 1000;
            _gameState.RiichiSticks++;

            if (_gameState.TurnCount <= _gameState.PlayerCount
                && _gameState.Players.All(p => p.Melds.Count == 0))
                player.IsDoubleRiichi = true;

            // 广播立直确认
            Broadcast(nameof(NotifyRiichi), new RiichiEventDto
            {
                Seat = player.Seat,
                NewScore = player.Score,
                RiichiSticksOnTable = _gameState.RiichiSticks,
                IsDoubleRiichi = player.IsDoubleRiichi
            });
        }

        _ = _engine.TriggerDiscardAction(_gameState, player, tileState);

        _gameState.EventLog.Add(new GameEvent
        {
            EventType = GameEvent.Discard, Seat = player.Seat,
            Payload = new() { ["tileCode"] = tileState.Tile.TileCode }
        });

        // 广播弃牌
        Broadcast(nameof(NotifyDiscard), new DiscardEventDto
        {
            Seat = player.Seat,
            Tile = RpcSerializer.ToDto(tileState),
            IsRiichi = isRiichi,
            IsTsumogiri = isTsumogiri
        });

        OpenReactionWindow(player.Seat, tileState);
    }

    /// <summary>打开反应窗口。</summary>
    private void OpenReactionWindow(int sourceSeat, MahjongTileState sourceTile)
    {
        _gameState.Phase = TurnPhase.ReactionPhase;
        BroadcastPhaseChange();

        var window = _gameState.ReactionWindow;
        window.IsOpen = true;
        window.SourceSeat = sourceSeat;
        window.SourceTileInstanceId = sourceTile.InstanceId;
        window.OpenedAtUtc = DateTime.UtcNow;
        window.Responses.Clear();

        bool anyoneHasActions = false;

        for (int i = 0; i < _gameState.PlayerCount; i++)
        {
            if (i == sourceSeat) continue;

            var player = _gameState.GetPlayer(i);
            var available = _engine.GetAvailableReactions(_gameState, player, sourceTile);

            if (available.HasAny)
            {
                anyoneHasActions = true;
                RpcId(_seatToPeer[i], nameof(NotifyReactionWindow),
                    RpcSerializer.Serialize(new ReactionWindowEventDto
                    {
                        SourceTile = RpcSerializer.ToDto(sourceTile),
                        SourceSeat = sourceSeat,
                        TimeoutSeconds = window.TimeoutSeconds,
                        AvailableActions = RpcSerializer.ToDto(available)
                    }));
            }
            else
            {
                window.SubmitPass(i);
            }
        }

        if (!anyoneHasActions)
        {
            window.Close();
            AdvanceToNextTurn();
        }
    }

    /// <summary>尝试结算反应窗口。</summary>
    private void TryResolveReactionWindow()
    {
        var window = _gameState.ReactionWindow;
        if (!window.IsOpen) return;

        int sourceSeat = window.SourceSeat ?? 0;
        if (!window.AllResponded(_gameState.PlayerCount, sourceSeat)) return;

        _gameState.Phase = TurnPhase.ResolutionPhase;
        BroadcastPhaseChange();

        var allCandidates = window.Responses
            .Where(kv => kv.Value != null && kv.Value.Count > 0)
            .SelectMany(kv => kv.Value!)
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => DistanceFromSource(c.Seat, sourceSeat, _gameState.PlayerCount))
            .ToList();

        window.Close();

        if (allCandidates.Count == 0) { AdvanceToNextTurn(); return; }

        ExecuteReactionWinner(allCandidates[0]);
    }

    /// <summary>执行反应窗口胜出动作。</summary>
    private void ExecuteReactionWinner(PlayerActionCandidate winner)
    {
        switch (winner.ActionType)
        {
            case "hu":
                int sourceSeat = _gameState.ReactionWindow.SourceSeat ?? -1;
                ServerRoundEnd("ron", winner.Seat, sourceSeat);
                return;

            case "peng": case "chi": case "gang":
                ExecuteMeld(winner);
                break;
        }

        Broadcast(nameof(NotifyReactionResult), new ReactionResultEventDto
        {
            WinnerSeat = winner.Seat,
            ActionType = winner.ActionType,
            OptionId = winner.Params.GetValueOrDefault("optionId")?.ToString() ?? ""
        });
    }

    /// <summary>执行副露。</summary>
    private void ExecuteMeld(PlayerActionCandidate candidate)
    {
        var player = _gameState.GetPlayer(candidate.Seat);
        var sourceTileId = _gameState.ReactionWindow.SourceTileInstanceId;
        int sourceSeat = _gameState.ReactionWindow.SourceSeat ?? -1;

        MahjongTileState? sourceTile = null;
        if (sourceSeat >= 0 && sourceTileId.HasValue)
        {
            var sourcePlayer = _gameState.GetPlayer(sourceSeat);
            sourceTile = sourcePlayer.Discards.FirstOrDefault(t => t.InstanceId == sourceTileId.Value);
            if (sourceTile != null) sourcePlayer.Discards.Remove(sourceTile);
        }
        if (sourceTile == null) return;

        string optionId = candidate.Params.GetValueOrDefault("optionId")?.ToString() ?? "";
        var validResult = _engine.Validators.ValidateAll(
            candidate.ActionType, _gameState, player, sourceTile);
        var option = validResult.Options.FirstOrDefault(o => o.OptionId == optionId)
            ?? validResult.Options.FirstOrDefault();

        var meldTiles = new List<MahjongTileState> { sourceTile };
        if (option != null)
        {
            foreach (var tileId in option.InvolvedTileIds)
            {
                var handTile = player.Hand.FirstOrDefault(t => t.InstanceId == tileId);
                if (handTile != null)
                {
                    player.Hand.Remove(handTile);
                    handTile.Zone = MahjongTileZone.Meld; handTile.IsLocked = true;
                    meldTiles.Add(handTile);
                }
            }
        }

        sourceTile.Zone = MahjongTileZone.Meld; sourceTile.IsLocked = true;

        string kind = candidate.ActionType switch
        {
            "chi" => MeldGroup.KindChi, "peng" => MeldGroup.KindPeng,
            "gang" => MeldGroup.KindMinkan, _ => candidate.ActionType
        };

        var meldGroup = new MeldGroup { Kind = kind, Tiles = meldTiles, SourceSeat = sourceSeat, IsOpen = true };
        player.Melds.Add(meldGroup);
        if (kind != MeldGroup.KindAnkan) player.IsMenzen = false;

        // 杠后：翻新宝牌 + 岭上摸牌
        if (candidate.ActionType == "gang")
        {
            ServerRevealDora();
            _gameState.Phase = TurnPhase.RinShanPhase;
            _gameState.CurrentTurnSeat = candidate.Seat;
            BroadcastPhaseChange();
            ServerDrawFromDeadWall(candidate.Seat);
        }
        else
        {
            _gameState.CurrentTurnSeat = candidate.Seat;
            _gameState.Phase = TurnPhase.DiscardPhase;
            BroadcastPhaseChange();
            BroadcastTurnStart(candidate.Seat);
        }
    }

    private void ExecuteSelfAction(PlayerState player, string actionType, string optionId,
        Game.Rules.Validators.ValidationResult validResult)
    {
        if (actionType == "hu")
        {
            ServerRoundEnd("tsumo", player.Seat, -1);
            return;
        }

        if (actionType == "gang")
        {
            var option = validResult.Options.FirstOrDefault(o => o.OptionId == optionId)
                ?? validResult.Options.FirstOrDefault();
            if (option == null) return;
            string kind = option.Extra.GetValueOrDefault("kind")?.ToString() ?? MeldGroup.KindAnkan;
            ExecuteSelfGang(player, option, kind);
        }
    }

    private void ExecuteSelfGang(PlayerState player, Game.Rules.Validators.ActionOption option, string kind)
    {
        var meldTiles = new List<MahjongTileState>();
        foreach (var tileId in option.InvolvedTileIds)
        {
            var handTile = player.Hand.FirstOrDefault(t => t.InstanceId == tileId);
            if (handTile != null)
            {
                player.Hand.Remove(handTile);
                handTile.Zone = MahjongTileZone.Meld; handTile.IsLocked = true;
                meldTiles.Add(handTile);
            }
        }

        if (kind == MeldGroup.KindKakan)
        {
            var existing = player.Melds.FirstOrDefault(m =>
                m.Kind == MeldGroup.KindPeng && m.Tiles.Count > 0 && meldTiles.Count > 0
                && m.Tiles[0].Tile.Category == meldTiles[0].Tile.Category
                && m.Tiles[0].Tile.Rank == meldTiles[0].Tile.Rank);

            if (existing != null)
            {
                var idx = player.Melds.IndexOf(existing);
                existing.Tiles.AddRange(meldTiles);
                player.Melds[idx] = new MeldGroup
                {
                    Kind = MeldGroup.KindKakan, Tiles = existing.Tiles,
                    SourceSeat = existing.SourceSeat, IsOpen = true
                };
            }
        }
        else
        {
            player.Melds.Add(new MeldGroup
            {
                Kind = kind, Tiles = meldTiles, SourceSeat = -1,
                IsOpen = kind != MeldGroup.KindAnkan
            });
        }

        Broadcast(nameof(NotifyReactionResult), new ReactionResultEventDto
        {
            WinnerSeat = player.Seat, ActionType = "gang", OptionId = option.OptionId,
            NewMeld = RpcSerializer.ToDto(player.Melds.Last())
        });

        ServerRevealDora();
        _gameState.Phase = TurnPhase.RinShanPhase;
        BroadcastPhaseChange();
        ServerDrawFromDeadWall(player.Seat);
    }

    /// <summary>岭上摸牌。</summary>
    private void ServerDrawFromDeadWall(int seat)
    {
        if (_gameState.DeadWall.Count == 0) { ServerRoundEnd("draw", null, -1); return; }

        var player = _gameState.GetPlayer(seat);
        var tileState = _gameState.DeadWall[0];
        _gameState.DeadWall.RemoveAt(0);

        tileState.Zone = MahjongTileZone.Hand; tileState.OwnerSeat = seat;
        player.Hand.Add(tileState);

        RpcId(_seatToPeer[seat], nameof(NotifyDraw), RpcSerializer.Serialize(new DrawEventDto
        {
            Seat = seat, Tile = RpcSerializer.ToDto(tileState, true),
            WallRemaining = _gameState.Wall.Count, IsRinshan = true
        }));
        string hidden = RpcSerializer.Serialize(new DrawEventDto
        {
            Seat = seat, Tile = RpcSerializer.ToDto(tileState, false),
            WallRemaining = _gameState.Wall.Count, IsRinshan = true
        });
        foreach (var kv in _seatToPeer.Where(kv => kv.Key != seat))
            RpcId(kv.Value, nameof(NotifyDraw), hidden);

        _gameState.Phase = TurnPhase.SelfActionPhase;
        BroadcastPhaseChange();

        var selfActions = _engine.GetAvailableSelfActions(_gameState, player, tileState);
        if (selfActions.HasAny)
            RpcId(_seatToPeer[seat], nameof(NotifySelfActions),
                RpcSerializer.Serialize(new SelfActionsEventDto { AvailableActions = RpcSerializer.ToDto(selfActions) }));
    }

    /// <summary>翻新宝牌指示牌（杠后）。</summary>
    private void ServerRevealDora()
    {
        if (_gameState.RevealedTiles.Count >= 5) return; // 最多 5 张宝牌指示牌
        if (_gameState.DeadWall.Count == 0) return;

        // 从王牌区取一张作为新宝牌指示牌
        var indicator = _gameState.DeadWall.Last();
        _gameState.DeadWall.Remove(indicator);
        indicator.Zone = MahjongTileZone.Reveal;
        indicator.IsFaceUp = true;
        _gameState.RevealedTiles.Add(indicator);

        Broadcast(nameof(NotifyDoraReveal), new DoraRevealEventDto
        {
            NewIndicator = RpcSerializer.ToDto(indicator),
            TotalDoraCount = _gameState.RevealedTiles.Count
        });
    }

    /// <summary>推进到下一回合。</summary>
    private void AdvanceToNextTurn()
    {
        foreach (var p in _gameState.Players) p.IsIppatsu = false;

        int next = _gameState.NextSeat(_gameState.CurrentTurnSeat);
        _gameState.CurrentTurnSeat = next;
        _gameState.TurnCount++;
        _gameState.GetPlayer(next).HasDrawnThisTurn = false;

        _gameState.Phase = TurnPhase.DrawPhase;
        BroadcastPhaseChange();
        BroadcastTurnStart(next);
        ServerDraw(next);
    }

    /// <summary>局结束。</summary>
    private void ServerRoundEnd(string reason, int? winnerSeat, int loserSeat)
    {
        _gameState.Phase = TurnPhase.RoundEnd;
        _gameState.IsRoundEnded = true;
        BroadcastPhaseChange();

        var dto = new RoundEndEventDto
        {
            Reason = reason,
            WinnerSeat = winnerSeat,
            LoserSeat = loserSeat >= 0 ? loserSeat : null
        };

        if (winnerSeat.HasValue)
        {
            var winner = _gameState.GetPlayer(winnerSeat.Value);
            dto.WinnerHand = winner.Hand.Select(t => RpcSerializer.ToDto(t)).ToList();

            // 从和牌验证的 Extra 中获取 yaku 列表和总番数
            // （由 HuValidator → YakuRuleRegistry.EvaluateAll 在验证时计算）
            int han = winner.Counters.GetValueOrDefault("last_win_han", 0);
            int fu = winner.Counters.GetValueOrDefault("last_win_fu", 30);
            bool isYakuman = winner.RuntimeTags.Contains("last_win_yakuman");
            bool isTsumo = reason == "tsumo";
            bool isDealer = winnerSeat.Value == _gameState.DealerSeat;

            if (han > 0)
            {
                var scorer = new Yaku.ScoreCalculator();
                dto.TotalHan = han;
                dto.Fu = fu;
                dto.IsYakuman = isYakuman;
                dto.BasePoints = scorer.CalculateBasePoints(han, fu, isYakuman);
                dto.ScoreChanges = scorer.CalculateScoreChanges(
                    _gameState, winnerSeat.Value, loserSeat >= 0 ? loserSeat : null,
                    han, fu, isYakuman, isTsumo);

                // 应用分数变化
                foreach (var (seat, change) in dto.ScoreChanges)
                    _gameState.GetPlayer(seat).Score += change;
            }
        }

        Broadcast(nameof(NotifyRoundEnd), dto);
    }

    /// <summary>断线重连全量同步。</summary>
    private void SendFullSync(int seat)
    {
        var initDto = BuildGameInitDto(seat);
        var fullSync = new FullSyncEventDto
        {
            InitState = initDto,
            CurrentPhase = _gameState.Phase.ToString(),
            CurrentTurnSeat = _gameState.CurrentTurnSeat,
            TurnCount = _gameState.TurnCount,
            IsReactionWindowOpen = _gameState.ReactionWindow.IsOpen
        };
        RpcId(_seatToPeer[seat], nameof(NotifyFullSync), RpcSerializer.Serialize(fullSync));
    }

    /// <summary>反应窗口超时。</summary>
    public void CheckReactionTimeout()
    {
        var window = _gameState.ReactionWindow;
        if (!window.IsOpen || window.OpenedAtUtc == null) return;
        if ((DateTime.UtcNow - window.OpenedAtUtc.Value).TotalSeconds < window.TimeoutSeconds) return;

        int sourceSeat = window.SourceSeat ?? 0;
        for (int i = 0; i < _gameState.PlayerCount; i++)
        {
            if (i == sourceSeat) continue;
            if (!window.Responses.ContainsKey(i) || window.Responses[i] == null)
                window.SubmitPass(i);
        }
        TryResolveReactionWindow();
    }

    // ═══════════════════════════════════════════
    // 广播辅助
    // ═══════════════════════════════════════════

    private void Broadcast<T>(string methodName, T dto)
    {
        string json = RpcSerializer.Serialize(dto);
        foreach (var kv in _seatToPeer)
            RpcId(kv.Value, methodName, json);
    }

    private void BroadcastPhaseChange()
    {
        Broadcast(nameof(NotifyPhaseChange), new PhaseChangeEventDto
        {
            Phase = _gameState.Phase.ToString(),
            CurrentSeat = _gameState.CurrentTurnSeat
        });
    }

    private void BroadcastTurnStart(int seat)
    {
        Broadcast(nameof(NotifyTurnStart), new TurnStartEventDto
        {
            Seat = seat,
            TurnCount = _gameState.TurnCount,
            Phase = _gameState.Phase.ToString()
        });
    }

    /// <summary>为某位玩家构建初始化 DTO（手牌只对自己可见）。</summary>
    private GameInitEventDto BuildGameInitDto(int forSeat)
    {
        var dto = new GameInitEventDto
        {
            MySeat = forSeat,
            DealerSeat = _gameState.DealerSeat,
            RoundWind = _gameState.RoundWind,
            Honba = _gameState.Honba,
            RiichiSticks = _gameState.RiichiSticks,
            WallRemaining = _gameState.Wall.Count,
            DoraIndicators = _gameState.RevealedTiles.Select(t => RpcSerializer.ToDto(t)).ToList()
        };

        foreach (var player in _gameState.Players)
        {
            var info = new PlayerInfoDto
            {
                Seat = player.Seat,
                CharacterId = _seatToCharacter.GetValueOrDefault(player.Seat, ""),
                Score = player.Score,
                SeatWind = player.SeatWind,
                IsRiichi = player.IsRiichi,
                HandCount = player.Hand.Count,
                Melds = player.Melds.Select(RpcSerializer.ToDto).ToList(),
                Discards = player.Discards.Select(t => RpcSerializer.ToDto(t)).ToList()
            };

            // 只有自己能看到手牌内容
            if (player.Seat == forSeat)
                info.HandTiles = player.Hand.Select(t => RpcSerializer.ToDto(t, true)).ToList();

            dto.Players.Add(info);
        }

        return dto;
    }

    // ═══════════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════════

    private int GetSeatByPeer(long peerId)
    {
        foreach (var kv in _seatToPeer) if (kv.Value == peerId) return kv.Key;
        return -1;
    }

    private void SendError(long peerId, string msg)
        => RpcId(peerId, nameof(NotifyError), msg);

    private MahjongTileState? FindSourceTile()
    {
        var id = _gameState.ReactionWindow.SourceTileInstanceId;
        return id == null ? null : _engine.FindTileById(_gameState, id.Value);
    }

    private static int GetActionPriority(string actionType) => actionType switch
    {
        "hu" => 100, "gang" => 50, "peng" => 40, "chi" => 10, _ => 30
    };

    private static int DistanceFromSource(int target, int source, int count)
        => (target - source + count) % count;

    // ═══════════════════════════════════════════
    // AI 入口（本地直接调用，不走网络，但走相同验证）
    // ═══════════════════════════════════════════

    /// <summary>AI 弃牌。与 RequestDiscard 相同逻辑，跳过 peerId 检查。</summary>
    public void HandleAiDiscard(int seat, string json)
    {
        var request = RpcSerializer.Deserialize<DiscardRequestDto>(json);
        if (request == null) return;
        if (!_engine.IsCurrentPlayer(_gameState, seat)) return;
        if (_gameState.Phase != TurnPhase.DiscardPhase && _gameState.Phase != TurnPhase.SelfActionPhase) return;

        var player = _gameState.GetPlayer(seat);
        if (!System.Guid.TryParse(request.TileInstanceId, out var tileId)) return;
        var tileState = _engine.FindTileInHand(player, tileId);
        if (tileState == null || tileState.IsLocked) return;

        ExecuteDiscard(player, tileState, request.IsRiichi);
    }

    /// <summary>AI 自摸动作。</summary>
    public void HandleAiSelfAction(int seat, string json)
    {
        var request = RpcSerializer.Deserialize<SelfActionRequestDto>(json);
        if (request == null) return;
        if (!_engine.IsCurrentPlayer(_gameState, seat) || _gameState.Phase != TurnPhase.SelfActionPhase) return;

        var player = _gameState.GetPlayer(seat);
        var lastDrawn = player.Hand.LastOrDefault();
        if (lastDrawn == null) return;

        var validResult = _engine.Validators.ValidateAll(request.ActionType, _gameState, player, lastDrawn);
        if (!validResult.IsValid) return;

        ExecuteSelfAction(player, request.ActionType, request.OptionId, validResult);
    }

    /// <summary>AI 反应。</summary>
    public void HandleAiReaction(int seat, string json)
    {
        var request = RpcSerializer.Deserialize<ReactionRequestDto>(json);
        if (request == null) return;
        if (_gameState.Phase != TurnPhase.ReactionPhase || !_gameState.ReactionWindow.IsOpen) return;
        if (seat == _gameState.ReactionWindow.SourceSeat) return;

        if (request.ActionType == "pass")
        {
            _gameState.ReactionWindow.SubmitPass(seat);
        }
        else
        {
            var sourceTile = FindSourceTile();
            if (sourceTile == null) return;

            var player = _gameState.GetPlayer(seat);
            var validResult = _engine.Validators.ValidateAll(request.ActionType, _gameState, player, sourceTile);
            if (!validResult.IsValid) return;

            var option = validResult.Options.FirstOrDefault(o => o.OptionId == request.OptionId)
                ?? validResult.Options.FirstOrDefault();

            _gameState.ReactionWindow.SubmitCandidate(seat, new PlayerActionCandidate
            {
                Seat = seat,
                ActionType = request.ActionType,
                Priority = GetActionPriority(request.ActionType),
                Params = new Dictionary<string, object> { ["optionId"] = option?.OptionId ?? "" }
            });
        }

        TryResolveReactionWindow();
    }
}