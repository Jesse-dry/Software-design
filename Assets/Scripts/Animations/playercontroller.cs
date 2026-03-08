using UnityEngine;

public class playercontroller : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5.0f;

    [Header("空气墙设置")]
    public float minX = -10.0f; // 左边最远能走到哪
    public float maxX = 10.0f;  // 右边最远能走到哪

    private SpriteRenderer spriteRenderer;
    private Animator animator;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 1. 获取按键输入
        float moveX = Input.GetAxisRaw("Horizontal");

        // 2. 计算移动并应用
        Vector3 moveDirection = new Vector3(moveX, 0);
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        // --- 空气墙逻辑开始 ---
        // 获取移动后的当前坐标
        Vector3 currentPos = transform.position;

        // 使用 Mathf.Clamp 限制 X 轴坐标
        // 它的意思是：如果 currentPos.x 小于 minX，就让它等于 minX；如果大于 maxX，就等于 maxX
        currentPos.x = Mathf.Clamp(currentPos.x, minX, maxX);

        // 把修正后的坐标重新赋给玩家
        transform.position = currentPos;
        // --- 空气墙逻辑结束 ---

        // 3. 翻转图片
        if (moveX > 0)
        {
            spriteRenderer.flipX = true; 
        }
        else if (moveX < 0)
        {
            spriteRenderer.flipX = false; 
        }

        // 4. 对接动画
        if (moveX != 0)
        {
            animator.SetBool("isWalking", true);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }
    }
}