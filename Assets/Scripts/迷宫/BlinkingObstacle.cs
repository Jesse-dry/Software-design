using UnityEngine;

public class BlinkingObstacle : MonoBehaviour
{
    public float blinkInterval = 0.5f; 
    
    // 我们新加的“开关”：
    // 勾选 = 幽灵模式（隐身也有碰撞）
    // 不勾选 = 简单模式（隐身就可以安全穿过去）
    public bool isGhostMode = true; 

    private SpriteRenderer spriteRenderer;
    private Collider2D myCollider; // 新增：用来控制碰撞体的开关
    private float timer = 0f;

    void Start()
    {
        // 游戏开始时，把控制图片和碰撞体的“遥控器”都拿在手里
        spriteRenderer = GetComponent<SpriteRenderer>();
        myCollider = GetComponent<Collider2D>(); 
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= blinkInterval)
        {
            // 切换图片的显示和隐藏
            spriteRenderer.enabled = !spriteRenderer.enabled;
            
            // 核心判断逻辑：
            if (isGhostMode)
            {
                // 如果是幽灵模式：碰撞体永远开着，随时会撞死
                myCollider.enabled = true; 
            }
            else
            {
                // 如果是简单模式：图片亮着就有碰撞，图片消失碰撞也就跟着消失
                myCollider.enabled = spriteRenderer.enabled; 
            }
            
            timer = 0f;
        }
    }
}