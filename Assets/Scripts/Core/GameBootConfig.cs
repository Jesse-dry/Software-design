using UnityEngine;

/// <summary>
/// 游戏启动配置 ScriptableObject。
/// 创建方式：Assets → Create → Game → Boot Config
/// 必须放在 Resources/Config/GameBootConfig.asset 路径下，供 Bootstrapper 自动加载。
/// </summary>
[CreateAssetMenu(fileName = "GameBootConfig", menuName = "Game/Boot Config")]
public class GameBootConfig : ScriptableObject
{
    [Header("Scene Names")]
    public string bootScene    = "Boot";
    public string abyssScene   = "Abyss";
    public string courtScene   = "Court";
    public string memoryScene  = "Memory";
    public string mainMenuScene = "MainMenu";

    [Header("Initial Phase")]
    public GamePhase startPhase = GamePhase.Abyss;

    [Header("Debug")]
    [Tooltip("启用后 Bootstrapper 会在 Console 输出初始化日志")]
    public bool verboseLog = true;
}
