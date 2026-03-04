using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public bool canMove = true;

    [Header("Axis Lock")]
    [Tooltip("启用后仅允许沿单轴移动")]
    public bool singleAxisMode = false;

    public enum MovementAxis { Horizontal, Vertical }
    [Tooltip("单轴模式下的移动轴")]
    public MovementAxis lockedAxis = MovementAxis.Vertical;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool receivedInputAction = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnMove(InputValue value)
    {
        if (canMove)
        {
            Vector2 raw = value.Get<Vector2>().normalized;
            receivedInputAction = true;

            if (singleAxisMode)
            {
                moveInput = lockedAxis == MovementAxis.Vertical
                    ? new Vector2(0f, raw.y)
                    : new Vector2(raw.x, 0f);
            }
            else
            {
                moveInput = raw;
            }
        }
    }

    void FixedUpdate()
    {
        // 如果没有通过 InputAction 收到输入，使用键盘回退（W/S 或 Up/Down）
        if (canMove && !receivedInputAction)
        {
            Vector2 kb = Vector2.zero;
            var kbState = UnityEngine.InputSystem.Keyboard.current;
            if (kbState != null)
            {
                if (kbState.wKey.isPressed || kbState.upArrowKey.isPressed) kb.y += 1f;
                if (kbState.sKey.isPressed || kbState.downArrowKey.isPressed) kb.y -= 1f;
                if (kbState.aKey.isPressed || kbState.leftArrowKey.isPressed) kb.x -= 1f;
                if (kbState.dKey.isPressed || kbState.rightArrowKey.isPressed) kb.x += 1f;
                if (singleAxisMode)
                    kb = lockedAxis == MovementAxis.Vertical ? new Vector2(0f, kb.y) : new Vector2(kb.x, 0f);
                moveInput = kb.normalized;
            }
            else
            {
                // Fallback to old Input.GetAxis if new input system not available
                float vx = UnityEngine.Input.GetAxisRaw("Horizontal");
                float vy = UnityEngine.Input.GetAxisRaw("Vertical");
                moveInput = singleAxisMode ? (lockedAxis == MovementAxis.Vertical ? new Vector2(0f, vy) : new Vector2(vx, 0f)) : new Vector2(vx, vy);
            }
        }

        rb.linearVelocity = canMove ? moveInput * moveSpeed : Vector2.zero;
    }

    public void Freeze()
    {
        canMove = false;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }

    public void Unfreeze()
    {
        canMove = true;
    }
}