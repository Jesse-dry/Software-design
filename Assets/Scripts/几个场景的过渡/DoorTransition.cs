using UnityEngine;
using UnityEngine.SceneManagement; // 【关键】引入 Unity 的场景切换工具箱

public class DoorTransition : MonoBehaviour
{
    [Header("这扇门通向哪个场景？（填场景的名字）")]
    public string targetSceneName;

    private bool canEnter = false; // 主角是不是正站在门前？

    void Update()
    {
        // 如果主角站在门前，并且玩家按下了 E 键
        if (canEnter && Input.GetKeyDown(KeyCode.E))
        {
            // 立刻切换到你填写的那个场景
            SceneManager.LoadScene(targetSceneName);
        }
    }

    // 当主角走进门的感应区
    void OnTriggerEnter2D(Collider2D other)
    {
        // 认准主角的身份证
        if (other.CompareTag("Player"))
        {
            canEnter = true;
            Debug.Log("主角站在门前了，可以按 E 键！");
        }
    }

    // 当主角离开门的感应区
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = false;
        }
    }
}