using UnityEngine;

/// <summary>
/// 场景 UI 根节点 — 放在每个场景的 UI Canvas 顶层。
/// 
/// 职责：
///   1. Awake 时自动向 UIManager 注册本场景的 UI 层引用
///   2. OnDestroy 时自动注销
///   3. 注册后触发场景子系统（HUD / Modal / Dialogue / ItemDisplay）重新初始化
/// 
/// 美术工作流：
///   1. 在场景中创建一个 Canvas，建议命名 UIRoot_场景名（如 UIRoot_Court）
///   2. Canvas 下创建子节点：HUDLayer / OverlayLayer / ModalLayer
///      • 每个子节点需挂 Canvas（overrideSorting=true）+ GraphicRaycaster
///      • 推荐 sortingOrder：HUD=10, Overlay=50, Modal=90
///   3. 挂上 UISceneRoot 脚本，将各层 RectTransform 拖入 Inspector
///   4. 保存为 Prefab 即可复用/定制
/// 
/// 排序约定：
///   全局层（Toast / Transition）sortingOrder ≥ 1000。
///   场景层 sortingOrder 应小于 1000，避免遮挡全局覆盖。
/// 
/// 模态背景：
///   如需自定义模态背景（半透明遮罩），在 ModalLayer 下创建名为
///   "ModalBackground" 的 Image 子节点；若不创建，UIManager 会自动生成默认版本。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/UISceneRoot")]
public class UISceneRoot : MonoBehaviour
{
    [Header("== 场景 UI 层（拖入 Inspector） ==")]
    [Tooltip("HUD 层：血条、状态灯、小地图等")]
    public RectTransform hudLayer;

    [Tooltip("Overlay 层：道具面板、暂停菜单等可叠加面板")]
    public RectTransform overlayLayer;

    [Tooltip("Modal 层：对话弹窗、确认框（打断游戏交互）")]
    public RectTransform modalLayer;

    private void Awake()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RegisterSceneRoot(this);
        }
        else
        {
            Debug.LogWarning(
                $"[UISceneRoot] UIManager 尚未就绪，无法注册场景根: {gameObject.name}\n" +
                "请确保 GameBootstrapper 已在 BeforeSceneLoad 阶段创建 UIManager。");
        }
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UnregisterSceneRoot(this);
        }
    }
}
