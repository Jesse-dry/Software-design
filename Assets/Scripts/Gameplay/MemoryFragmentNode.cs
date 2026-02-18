using System.Buffers;
using UnityEngine;

public class MemoryFragmentNode : MemoryNodeBase
{
    [TextArea(3, 6)]
    public string memoryText;

    public override void Interact()
    {
        base.Interact();
        MemoryUIManager.Instance.ShowMemory(this);
    }
}