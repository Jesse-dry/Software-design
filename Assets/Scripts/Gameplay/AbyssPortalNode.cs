using UnityEngine;

/// <summary>
/// 潜渊门节点（挂在 AbyssPortal 同一 GameObject 上）。
/// 继承 MemoryNodeBase 以复用 PlayerInteraction 的 Trigger 检测 + 按 E 交互机制。
/// Interact() 时委托给 AbyssPortal.TryEnterAbyss()。
/// </summary>
[RequireComponent(typeof(AbyssPortal))]
public class AbyssPortalNode : MemoryNodeBase
{
    private AbyssPortal portal;

    private void Awake()
    {
        portal = GetComponent<AbyssPortal>();
    }

    public override void Interact()
    {
        portal?.TryEnterAbyss();
    }
}
