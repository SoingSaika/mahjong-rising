using System.Linq;
using System.Threading.Tasks;
using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.Actions;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Game.Rules;

/// <summary>
/// MahjongRuleEngine 的辅助方法（partial class）。
/// </summary>
public partial class MahjongRuleEngine
{
    public bool IsCurrentPlayer(MahjongGameState gameState, int seat)
        => gameState.CurrentTurnSeat == seat;

    public MahjongTileState? FindTileInHand(PlayerState player, System.Guid tileInstanceId)
        => player.Hand.FirstOrDefault(x => x.InstanceId == tileInstanceId);

    public MahjongTileState? FindTileById(MahjongGameState gameState, System.Guid tileInstanceId)
    {
        // 在所有区域搜索
        foreach (var p in gameState.Players)
        {
            var t = p.Hand.FirstOrDefault(x => x.InstanceId == tileInstanceId);
            if (t != null) return t;
            t = p.Discards.FirstOrDefault(x => x.InstanceId == tileInstanceId);
            if (t != null) return t;
            foreach (var meld in p.Melds)
            {
                t = meld.Tiles.FirstOrDefault(x => x.InstanceId == tileInstanceId);
                if (t != null) return t;
            }
        }
        return gameState.Wall.FirstOrDefault(x => x.InstanceId == tileInstanceId)
            ?? gameState.DeadWall.FirstOrDefault(x => x.InstanceId == tileInstanceId)
            ?? gameState.RevealedTiles.FirstOrDefault(x => x.InstanceId == tileInstanceId);
    }

    // ── 牌特殊能力触发 ──

    public Task TriggerDiscardAction(MahjongGameState gameState, PlayerState player, MahjongTileState tileState)
    {
        if (tileState.Tile.Action == null) return Task.CompletedTask;
        var context = new MahjongActionContext(gameState, player, tileState);
        return tileState.Tile.Action.OnDiscard(context);
    }

    public Task TriggerDrawAction(MahjongGameState gameState, PlayerState player, MahjongTileState tileState)
    {
        if (tileState.Tile.Action == null) return Task.CompletedTask;
        var context = new MahjongActionContext(gameState, player, tileState);
        return tileState.Tile.Action.OnDraw(context);
    }

    public Task TriggerChiAction(MahjongGameState gameState, PlayerState player, MahjongTileState tileState)
    {
        if (tileState.Tile.Action == null) return Task.CompletedTask;
        var context = new MahjongActionContext(gameState, player, tileState);
        return tileState.Tile.Action.OnChi(context);
    }

    public Task TriggerPengAction(MahjongGameState gameState, PlayerState player, MahjongTileState tileState)
    {
        if (tileState.Tile.Action == null) return Task.CompletedTask;
        var context = new MahjongActionContext(gameState, player, tileState);
        return tileState.Tile.Action.OnPeng(context);
    }

    public Task TriggerGangAction(MahjongGameState gameState, PlayerState player, MahjongTileState tileState)
    {
        if (tileState.Tile.Action == null) return Task.CompletedTask;
        var context = new MahjongActionContext(gameState, player, tileState);
        return tileState.Tile.Action.OnGang(context);
    }
}