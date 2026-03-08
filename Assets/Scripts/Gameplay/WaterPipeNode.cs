using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // 必须引入这个以支持延迟协程

public class WaterPipeNode : MonoBehaviour
{
    [Header("基础配置")]
    public string pipeName = "破裂的主水管";

    [TextArea(2, 4)]
    public string promptText = "警告：水压正在暴增！如果不及时修复，该区域将面临全面淹没的风险。\n\n是否立即关闭前端阀门，并开始【水管连线】作业？";

    [Header("跳转配置")]
    [Tooltip("接水管小游戏场景的真实英文名")]
    public string targetSceneName = "ConnectPipe";

    [Header("高级交互特性")]
    [Tooltip("把用来做漏水特效的 Particle System 拖到这里！")]
    public ParticleSystem waterLeakParticles;

    private bool _isPlayerInRange = false;
    private bool _isRepairing = false; // 防止玩家疯狂连按E键

    private void Update()
    {
        // 如果不在范围内，或者已经在转场了，就什么也不做
        if (!_isPlayerInRange || _isRepairing) return;

        // 检测交互键 E
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
        {
            Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !_isRepairing)
        {
            _isPlayerInRange = true;
            if (UIManager.Instance != null && UIManager.Instance.Toast != null)
            {
                UIManager.Instance.Toast.Show("按 [E] 紧急修复水管");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
        }
    }

    private void Interact()
    {
        // 弹出极客风的确认框
        if (UIManager.Instance != null && UIManager.Instance.Modal != null)
        {
            string content = $"【{pipeName}】\n{promptText}\n\n(点击【确认】切断水流并开始修理)";
            UIManager.Instance.Modal.ShowConfirm(content, StartRepair);
        }
    }

    private void StartRepair()
    {
        // 标记为正在修理，锁定玩家的重复操作
        _isRepairing = true;

        // 开启炫酷的视觉过渡协程！
        StartCoroutine(RepairTransition());
    }

    private IEnumerator RepairTransition()
    {
        // 【特性 1】：瞬间切断漏水特效！物理反馈拉满！
        if (waterLeakParticles != null)
        {
            waterLeakParticles.Stop();
        }

        // 【特性 2】：弹出绿色的正面反馈提示
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            // 注意：这里用的是你修复后的 colorType 写法
            UIManager.Instance.Toast.Show("前置阀门已关闭！正在接入管线系统...", colorType: ToastColor.Positive);
        }

        // 【特性 3】：停顿 1.5 秒，让玩家好好欣赏一下水停了的画面
        yield return new WaitForSeconds(1.5f);

        Debug.Log($"[WaterPipe] 视觉过渡完成，正在跳转至 {targetSceneName}...");

        // 极其干脆的直接跳转，不需要去改 GameManager
        SceneManager.LoadScene(targetSceneName);
    }
}