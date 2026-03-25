namespace MahjongRising.code.Game;

public enum MahjongTileZone
{
    Wall,       // 牌山
    Hand,       // 手牌
    Discard,    // 弃牌区 / 牌河
    Meld,       // 吃碰杠区
    DeadWall,   // 岭上 / 王牌区
    Reveal,     // 展示区
    Removed     // 已移出本局
}
