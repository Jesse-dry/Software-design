using UnityEngine;

public class MemoryNodeBase : MonoBehaviour
{
    public string nodeID = "MemoryNode";

    public virtual void Interact()
    {
        Debug.Log("Interact with " + nodeID);
    }
}