using UnityEngine;

/// <summary>
/// 游戏启动配置 ScriptableObject。
/// 
/// 创建方式：Assets → Create → Game → Boot Config
/// 必须放在 Resources/Config/GameBootConfig.asset 路径下，供 Bootstrapper 自动加载。
/// 
/// 团队开发指南：
///   1. 新增场景：在下面添加场景名字段
///   2. 修改起始阶段：更改 startPhase
///   3. 调试日志：开启 verboseLog 查看初始化信息
/// </summary>
[CreateAssetMenu(fileName = "GameBootConfig", menuName = "Config/Game Boot Config")]
public class GameBootConfig : ScriptableObject
{
    [Header("场景名称 - 确保与 Build Settings 中的场景名一致")]
    [Tooltip("启动场景（几乎为空，只用于初始化）")]
    public string bootScene = "Boot";
    
    [Tooltip("主菜单场景")]
    public string mainMenuScene = "MainMenu";
    
    [Tooltip("过场动画场景")]
    public string cutsceneScene = "CutsceneScene";
    
    [Tooltip("记忆探索场景（原皮主角，收集碎片）")]
    public string memoryScene = "Memory";
    
    [Tooltip("潜渊场景（包含接水管、走廊等子区域）")]
    public string abyssScene = "Abyss";
    
    [Tooltip("庭审场景")]
    public string courtScene = "Court";

    [Tooltip("走廊迷宫场景（从 Abyss Hub 进入）")]
    public string corridorScene = "Corridor";

    [Tooltip("拷贝小游戏场景（所有路线必经 → Court）")]
    public string decodeGameScene = "DecodeGame";

    [Tooltip("水管房间入口场景（从 Abyss Hub 进入）")]
    public string pipeRoomScene = "PipeRoom";

    [Tooltip("接水管谜题场景（从 PipeRoom 进入）")]
    public string pipePuzzleScene = "PipePuzzle";

    [Tooltip("服务器室 Q&A 场景（从 Abyss Hub 进入）")]
    public string serverRoomScene = "ServerRoom";

    [Header("初始阶段 - 决定游戏从哪个阶段开始")]
    [Tooltip("开发时可以从任意阶段开始测试")]
    public GamePhase startPhase = GamePhase.Boot;

    [Header("调试")]
    [Tooltip("启用后 Bootstrapper 会在 Console 输出初始化日志")]
    public bool verboseLog = true;

    // ── 开发辅助 ─────────────────────────────────────────────

    /// <summary>
    /// 快速跳转到指定阶段（用于开发测试）
    /// 在 Inspector 中右键点击此 Asset 可以调用
    /// </summary>
    [ContextMenu("设为从 Boot 开始")]
    private void SetStartToBoot() { startPhase = GamePhase.Boot; }
    
    [ContextMenu("设为从 MainMenu 开始")]
    private void SetStartToMainMenu() { startPhase = GamePhase.MainMenu; }
    
    [ContextMenu("设为从 Memory 开始")]
    private void SetStartToMemory() { startPhase = GamePhase.Memory; }
    
    [ContextMenu("设为从 Abyss 开始")]
    private void SetStartToAbyss() { startPhase = GamePhase.Abyss; }
    
    [ContextMenu("设为从 Court 开始")]
    private void SetStartToCourt() { startPhase = GamePhase.Court; }

    [ContextMenu("设为从 Corridor 开始")]
    private void SetStartToCorridor() { startPhase = GamePhase.Corridor; }

    [ContextMenu("设为从 PipeRoom 开始")]
    private void SetStartToPipeRoom() { startPhase = GamePhase.PipeRoom; }

    [ContextMenu("设为从 ServerRoom 开始")]
    private void SetStartToServerRoom() { startPhase = GamePhase.ServerRoom; }

    [ContextMenu("设为从 DecodeGame 开始")]
    private void SetStartToDecodeGame() { startPhase = GamePhase.DecodeGame; }
}
