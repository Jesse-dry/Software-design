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

        if (interaction.currentNode != null)
        {
            interaction.currentNode.Interact();
        }
    }
}