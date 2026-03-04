using UnityEngine;

public class CameraBlink : MonoBehaviour
{
    [Header("闪烁时间设置")]
    public float onTime = 2.0f;  // 灯亮着的时间（秒）
    public float offTime = 2.0f; // 灯熄灭的时间（秒）

    private float timer = 0f;
    private bool isLightOn = true; // 当前灯是不是亮着的

    private SpriteRenderer lightSprite;
    private Collider2D lightCollider;

    void Start()
    {
        // 游戏开始时，获取灯效的图片和感应框
        lightSprite = GetComponent<SpriteRenderer>();
        lightCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        // 计时器：让时间一点点增加
        timer += Time.deltaTime; 

        if (isLightOn)
        {
            // 如果灯亮着，且时间超过了设定的亮灯时间
            if (timer >= onTime)
            {
                TurnOffLight(); // 关灯
            }
        }
        else
        {
            // 如果灯灭着，且时间超过了设定的关灯时间
            if (timer >= offTime)
            {
                TurnOnLight();  // 开灯
            }
        }
    }

    // 关灯的动作
    void TurnOffLight()
    {
        isLightOn = false;
        timer = 0f; // 计时器清零，重新算时间
        if (lightSprite != null) lightSprite.enabled = false; // 隐藏光束图片
        if (lightCollider != null) lightCollider.enabled = false; // 关闭感应区（安全了）
    }

    // 开灯的动作
    void TurnOnLight()
    {
        isLightOn = true;
        timer = 0f; // 计时器清零
        if (lightSprite != null) lightSprite.enabled = true; // 显示光束图片
        if (lightCollider != null) lightCollider.enabled = true; // 开启感应区（危险！）
    }
}