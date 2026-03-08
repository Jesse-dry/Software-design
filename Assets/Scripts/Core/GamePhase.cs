/// <summary>
/// 游戏阶段枚举。
/// 
/// 游戏整体流程：
///   Boot → MainMenu → Cutscene → Memory → Abyss(Hub) → 子场景分支 → DecodeGame → Court → Result
/// 
/// Abyss Hub 子场景分支：
///   Corridor  — 走廊迷宫（胜利获得 权杖 卡）
///   PipeRoom  — 水管房间入口 → PipePuzzle 接水管谜题（胜利获得 星币 卡）
///   ServerRoom — 服务器室 Q&A（第 2 题答对获得 宝剑 卡）
///   DecodeGame — 拷贝小游戏（胜利获得 圣杯 卡）→ 所有路线必经 → Court
/// 
/// 团队开发说明：
///   - 新增阶段：在此添加枚举值
///   - 修改 GameManager.EnterNewPhase() 添加新阶段的处理逻辑
///   - 修改 SceneController 添加对应场景的加载方法
/// </summary>
public enum GamePhase
{
    Boot,           // 启动：初始化系统资源
    MainMenu,       // 主菜单：开始游戏、设置、退出
    Cutscene,       // 过场动画：剧情展示
    Memory,         // 记忆探索：原皮主角，收集碎片
    Abyss,          // 潜渊 Hub：收集线索，选择门进入子场景
    Corridor,       // 走廊迷宫（从 Abyss 进入，胜利→权杖卡→返回 Abyss）
    PipeRoom,       // 水管房间入口（从 Abyss 进入，过渡到 PipePuzzle）
    PipePuzzle,     // 接水管谜题（胜利→星币卡→返回 Abyss）
    ServerRoom,     // 服务器室 Q&A（从 Abyss 进入，胜利→宝剑卡→返回 Abyss）
    DecodeGame,     // 拷贝小游戏（所有路线必经，胜利→圣杯卡→Court）
    Court,          // 庭审：举证/辩论阶段
    Result,         // 结局：展示结果
}
