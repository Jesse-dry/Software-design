using UnityEngine;

public class PlayerHide : MonoBehaviour
{
    [Header("图层设置")]
    public int normalLayer = 5;  // 正常走路时的层级（必须比花瓶大，比如 5）
    public int hideLayer = -1;   // 躲藏时的层级（必须比花瓶小，比如 -1）

    private bool isNearVase = false; // 是否在花瓶旁边
    private bool isHiding = false;   // 当前是否正在躲藏
    private Transform vaseTransform; // 记录当前碰到的是哪个花瓶

    private SpriteRenderer spriteRenderer;
    private playercontroller moveScript; // 获取你之前的移动脚本

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // 获取主角身上的移动控制脚本，方便躲藏时禁用它
        moveScript = GetComponent<playercontroller>();
        
        // 游戏开始时，确保是正常图层
        spriteRenderer.sortingOrder = normalLayer;
    }

    void Update()
    {
        // 如果在花瓶旁边，并且按下了键盘的 H 键
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("成功检测到 H 键被按下了！");
        }
        if (isNearVase && Input.GetKeyDown(KeyCode.H))
        {
            if (isHiding == false)
            {
                // —— 【开始躲藏】 ——
                isHiding = true;
                
                // 1. 图层变小，跑到花瓶后面
                spriteRenderer.sortingOrder = hideLayer; 
                
                // 2. 把人物强行吸附到花瓶的正中心 (X轴对齐)
                transform.position = new Vector3(vaseTransform.position.x, transform.position.y, transform.position.z);
                
                // 3. 关掉移动脚本，防止玩家在花瓶后面还能乱跑
                if (moveScript != null) moveScript.enabled = false;
            }
            else
            {
                // —— 【出来，解除躲藏】 ——
                isHiding = false;
                
                // 1. 图层变大，重新回到花瓶前面
                spriteRenderer.sortingOrder = normalLayer; 
                
                // 2. 重新开启移动脚本，可以继续走路了
                if (moveScript != null) moveScript.enabled = true;
            }
        }
    }

    // 当主角走入花瓶的感应区
    void OnTriggerEnter2D(Collider2D other)
    {
        // 检查碰到的东西是不是贴了 "Vase" 标签
        if (other.CompareTag("Vase")) 
        {
            isNearVase = true;
            vaseTransform = other.transform; // 记下这个花瓶的位置
        }
    }

    // 当主角离开花瓶的感应区
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Vase"))
        {
            isNearVase = false;
            vaseTransform = null;
        }
    }
}