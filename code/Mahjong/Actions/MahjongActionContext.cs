using MahjongRising.code.Game.State;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Mahjong.Actions;

public class MahjongActionContext
{
    public MahjongGameState GameState { get; init; }

    public PlayerState Owner { get; init; }

    public MahjongTileState TileState { get; init; }

    public bool IsServerAuthoritative { get; init; } = true;

    public MahjongActionContext(
        MahjongGameState gameState,
        PlayerState owner,
        MahjongTileState tileState)
    {
        GameState = gameState;
        Owner = owner;
        TileState = tileState;
    }
}