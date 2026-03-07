using UnityEngine;

public class DoorToCorridorNode : MonoBehaviour
{
    [Header("视觉表现")]
    [Tooltip("拖入带有 2D 灯光的子物体")]
    public GameObject doorLightObject;

    private bool _isPlayerInRange = false;

    private void Start()
    {
        // 游戏开始时，让感应灯保持关闭
        if (doorLightObject != null)
        {
            doorLightObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!_isPlayerInRange) return;

        // 按 E 键开门
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
        {
            if (GameManager.Instance != null)
            {
                // 瞬间跳转到走廊！
                GameManager.Instance.EnterPhase(GamePhase.Corridor);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;

            // 【特效触发】玩家靠近，瞬间点亮感应门！
            if (doorLightObject != null) doorLightObject.SetActive(true);

            if (UIManager.Instance != null && UIManager.Instance.Toast != null)
            {
                UIManager.Instance.Toast.Show("按 [E] 开启气闸门前往走廊");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;

            // 【特效触发】玩家离开，门灯熄灭
            if (doorLightObject != null) doorLightObject.SetActive(false);
        }
    }
}