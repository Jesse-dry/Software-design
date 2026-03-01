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

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检查碰到的是不是玩家（需要给玩家的 GameObject 加上 "Player" 标签）
        if (other.CompareTag("Player"))
        {
            Debug.Log("玩家拾取了一个记忆碎片！");

            // 通知传送点：碎片数 +1
            AbyssPortal.Instance.CollectFragment();

            // 播放一个拾取音效或粒子特效（后续可加）
        }
    }
}