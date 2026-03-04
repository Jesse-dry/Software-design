using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家交互检测。通过 Trigger2D 检测附近的 MemoryNodeBase，按交互键触发。
/// 现在不再依赖 InteractionPromptUI，改为各节点自行通过 ToastSystem 飘字。
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private InputAction interactAction;

    public MemoryNodeBase currentNode;

    [Header("Legacy Prompt（可选，留空则不使用）")]
    public InteractionPromptUI promptUI;

    private void OnEnable()
    {
        // 如果没有在 Inspector 绑定 InputAction，则尝试从 PlayerInput 查找
        if (interactAction == null)
        {
            var pi = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null && pi.actions != null)
            {
                var act = pi.actions.FindAction("Interact");
                if (act != null) interactAction = act;
            }
        }

        if (interactAction != null)
        {
            interactAction.Enable();
            interactAction.performed += OnInteractPerformed;
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
            interactAction.Disable();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (currentNode != null)
        {
            currentNode.Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<MemoryNodeBase>(out var node))
        {
            currentNode = node;
            promptUI?.Show();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<MemoryNodeBase>(out var node) && currentNode == node)
        {
            currentNode = null;
            promptUI?.Hide();
        }
    }
}