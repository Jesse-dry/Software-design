using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private InputAction interactAction;

    public MemoryNodeBase currentNode;
    public InteractionPromptUI promptUI;

    private void OnEnable()
    {
        interactAction.Enable();
        interactAction.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        interactAction.performed -= OnInteractPerformed;
        interactAction.Disable();
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (currentNode != null)
        {
            currentNode.Interact();
            // 可选：交互后清空节点引用
            // currentNode = null;
        }
    }

    // 检测进入可交互区域
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<MemoryNodeBase>(out var node))
        {
            currentNode = node;
            promptUI.Show();
        }
    }

    // 检测离开可交互区域
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<MemoryNodeBase>(out var node) && currentNode == node)
        {
            currentNode = null;
            promptUI.Hide();
        }
    }
}