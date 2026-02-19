using UnityEngine;

public class MemoryNodeBase : MonoBehaviour
{
    [TextArea(3, 10)]
    public string memoryContent;

    public virtual void Interact()
    {
        Debug.Log("Interact with " + gameObject.name);

        MemoryUIManager.Instance.ShowMemory(this as MemoryFragmentNode);
    }
}