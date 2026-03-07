using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections;          

public class LightDetection : MonoBehaviour
{
    [Header("【把黑布 BlackImage 拖到下面这个槽位里】")]
    public GameObject blackScreenUI; // 这一句就是那个“指定槽位”

    private AudioSource audioSource; 
    private bool hasFailed = false;  

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // 保险起见：游戏刚开始时，强行把黑布的勾取消掉（隐藏）
        if (blackScreenUI != null) blackScreenUI.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Player")) CheckAndFail(other.gameObject); }
    void OnTriggerStay2D(Collider2D other)  { if (other.CompareTag("Player")) CheckAndFail(other.gameObject); }
    void OnCollisionEnter2D(Collision2D collision) { if (collision.gameObject.CompareTag("Player")) CheckAndFail(collision.gameObject); }
    void OnCollisionStay2D(Collision2D collision)  { if (collision.gameObject.CompareTag("Player")) CheckAndFail(collision.gameObject); }

    void CheckAndFail(GameObject playerObj)
    {
        if (hasFailed) return; 

        PlayerHide hideScript = playerObj.GetComponent<PlayerHide>();
        if (hideScript != null && hideScript.isHiding) return; 

        hasFailed = true; 

        if (audioSource != null) audioSource.Play();
        GameObject mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCamera != null)
        {
            AudioSource bgmSource = mainCamera.GetComponent<AudioSource>();
            if (bgmSource != null) bgmSource.Stop(); 
        }

        playercontroller playerMove = playerObj.GetComponent<playercontroller>();
        if (playerMove != null) playerMove.enabled = false; 
        Animator playerAnim = playerObj.GetComponent<Animator>();
        if (playerAnim != null) playerAnim.enabled = false;

        StartCoroutine(BlackScreenAndRestart());
    }

IEnumerator BlackScreenAndRestart()
    {
        Debug.Log("🚨 1. 抓捕触发！准备开启黑屏！");

        if (blackScreenUI == null)
        {
            Debug.LogError("❌ 致命报错：你撞到的【这个】保安，他的 'Black Screen UI' 槽位是空的！请仔细检查你撞的是谁！");
        }
        else
        {
            Debug.Log("✅ 2. 槽位里有黑布！准备给黑布打勾！");
            blackScreenUI.SetActive(true);
            
            // 让电脑自己检查到底打上勾没有！
            if (blackScreenUI.activeSelf)
            {
                Debug.Log("✅ 3. 打勾成功！如果还没看到黑屏，绝对是因为 DeathCanvas 被隐藏了，或者图片透明度是 0！");
            }
            else
            {
                Debug.LogError("❌ 4. 见鬼了！打了勾却没生效！请检查它的父级 DeathCanvas 是不是没打勾？");
            }
        }

        yield return new WaitForSeconds(2.0f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}