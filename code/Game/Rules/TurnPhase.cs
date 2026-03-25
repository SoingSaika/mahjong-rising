namespace MahjongRising.code.Game.Rules;

public enum TurnPhase
{
    RoundStart,        // 配牌阶段
    DrawPhase,         // 摸牌阶段
    SelfActionPhase,   // 自摸后决策（暗杠/加杠/立直/自摸胡）
    DiscardPhase,      // 弃牌阶段
    ReactionPhase,     // 他家反应（吃/碰/杠/荣和）
    ResolutionPhase,   // 反应结算
    RinShanPhase,      // 岭上摸牌（杠后补牌）
    RoundEnd           // 局结束
}