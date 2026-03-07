using UnityEngine;

/// <summary>
/// 潜渊门交互节点。
/// 继承 MemoryNodeBase，通过 PlayerInteraction 统一检测 + 按 E 交互。
///
/// 碎片集齐时提示 "按 E 进入深渊"，否则提示 "碎片不足(x/n)"。
/// 按 E 后由 AbyssPortal.TryEnterAbyss() 接管流程。
/// </summary>
[RequireComponent(typeof(AbyssPortal))]
public class AbyssPortalNode : MemoryNodeBase
{
    [Tooltip("提示文字偏移（相对自身 transform.position）")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    [Tooltip("碎片集齐后的交互提示文字")]
    public string readyText = "按 E 进入深渊";

    [Tooltip("碎片不足时的提示文字")]
    public string insufficientText = "碎片不足，继续探索吧";

    private AbyssPortal _portal;

    private void Awake()
    {
        _portal = GetComponent<AbyssPortal>();
    }

    public override void Interact()
    {
        HidePrompt();
        _portal?.TryEnterAbyss();
    }

    public override void OnPlayerEnter(GameObject player)
    {
        if (_portal == null) return;

        string text = _portal.CurrentFragments >= _portal.RequiredFragments
            ? readyText
            : insufficientText;

        // 提示位置：必须在相机视野内（相机固定在原点，门在 Y=19 远超视野）
        var cam = Camera.main;
        Vector3 pos;
        if (cam != null)
            pos = new Vector3(0f, cam.transform.position.y + cam.orthographicSize * promptCameraYRatio, 0f);
        else
            pos = transform.position + promptOffset;

        ShowPrompt(text, pos);
    }

    public override void OnPlayerExit(GameObject player)
    {
        HidePrompt();
    }
}
