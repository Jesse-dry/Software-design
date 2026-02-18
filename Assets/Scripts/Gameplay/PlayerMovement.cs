using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public bool canMove = true;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnMove(InputValue value)
    {   if (canMove)
        {
            moveInput = value.Get<Vector2>().normalized;
        }
        

    }

    void FixedUpdate()
    {
        rb.linearVelocity = canMove ? moveInput * moveSpeed : Vector2.zero;
    }

    public void Freeze()
    {
        canMove = false;
        rb.linearVelocity = Vector2.zero;
    }

    public void Unfreeze()
    {
        canMove = true;
    }
}