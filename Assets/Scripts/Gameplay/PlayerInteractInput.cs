using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInteraction))]
public class PlayerInteractInput : MonoBehaviour
{
    private PlayerInteraction interaction;

    void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        Debug.Log("Try show UI");

        if (interaction.currentNode != null)
        {
            MemoryUIManager.Instance.ShowMemory(interaction.currentNode as MemoryFragmentNode);
            
            interaction.currentNode.Interact();
        }
    }
}