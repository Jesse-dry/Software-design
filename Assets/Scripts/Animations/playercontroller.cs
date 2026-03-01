using UnityEngine;

public class playercontroller : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5.0f;

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
        // Horizontal 是 A/D 键（左右）
        float moveX = Input.GetAxisRaw("Horizontal");

        // 2. 计算移动方向并移动
        // 用 Vector3 把左右和上下组合起来
        // .normalized 的作用是：防止你同时按住 W 和 D 斜着走的时候，速度叠加变得像起飞一样快
        Vector3 moveDirection = new Vector3(moveX, 0);
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        // 3. 翻转图片（只受左右移动影响）
        if (moveX > 0)
        {
            spriteRenderer.flipX = true; // 往右走，翻转图片
        }
        else if (moveX < 0)
        {
            spriteRenderer.flipX = false; // 往左走，保持原样
        }

        // 4. 对接动画（按你的要求，只有左右走才播放动画，上下走不播放）
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