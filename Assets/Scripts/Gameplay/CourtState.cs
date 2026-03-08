/// <summary>
/// 庭审阶段状态枚举。
/// </summary>
public enum CourtState
{
    Inactive,       // 未激活
    RulePanel,      // 显示规则属性版面
    NPCSpeech,      // NPC 发言
    AkanaMenu,      // 选择阿卡那牌
    CardDetail,     // 卡牌详情（打出 / 回退）
    SelectTarget,   // 选择目标 NPC
    RoundResult,    // 回合结算
    Victory,        // 胜利
    Defeat,         // 失败
}