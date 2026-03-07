/// <summary>
/// 游戏阶段枚举。
/// 
/// 游戏整体流程：
///   Boot → MainMenu → Cutscene → Memory → Abyss → Court → Result
/// 
/// 团队开发说明：
///   - 新增阶段：在此添加枚举值
///   - 修改 GameManager.EnterNewPhase() 添加新阶段的处理逻辑
///   - 修改 SceneController 添加对应场景的加载方法
/// </summary>
public enum GamePhase
{
    Boot,       // 启动：初始化系统资源
    MainMenu,   // 主菜单：开始游戏、设置、退出
    Cutscene,   // 过场动画：剧情展示
    Memory,     // 记忆探索：原皮主角，收集碎片
    Abyss,      // 潜渊：包含接水管、走廊等子区域
    Court,      // 庭审：举证/辩论阶段
    Result,     // 结局：展示结果
    DecodeGame,    //拷贝时的小游戏/
    Corridor      //走廊
}
