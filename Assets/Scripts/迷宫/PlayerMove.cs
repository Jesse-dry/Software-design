using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    void Update()
    {
        // 让小球跟着鼠标
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        transform.position = mousePos;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 找到游戏管理员
        GameManager1 manager = FindAnyObjectByType<GameManager1>();

        if (other.CompareTag("Wall"))
        {
            manager.GameOver(); // 告诉管理员：撞墙了，结束吧！
        }
        else if (other.CompareTag("Finish"))
        {
            manager.WinGame(); // 告诉管理员：到终点了！
        }
    }
}