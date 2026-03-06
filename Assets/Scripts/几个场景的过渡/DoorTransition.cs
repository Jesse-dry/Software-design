using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTransition : MonoBehaviour
{
    [Header("这扇门通向哪个场景？")]
    public string targetSceneName;

    [Header("把提示按E键的文字/图片拖到这里")]
    public GameObject ePromptUI; // 【新增】用来存放你的提示牌

    private bool canEnter = false;

    void Start()
    {
        // 游戏开始时，确保提示牌是隐藏的
        if (ePromptUI != null) ePromptUI.SetActive(false);
    }

    void Update()
    {
        if (canEnter && Input.GetKeyDown(KeyCode.E))
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }

    // 主角走近门
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = true;
            // 【新增】把提示牌的小勾勾打上（显示出画面）
            if (ePromptUI != null) ePromptUI.SetActive(true); 
        }
    }

    // 主角离开门
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = false;
            // 【新增】把提示牌的小勾勾取消掉（隐藏出画面）
            if (ePromptUI != null) ePromptUI.SetActive(false); 
        }
    }
}