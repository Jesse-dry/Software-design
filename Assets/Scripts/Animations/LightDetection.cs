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

        // 调试开关：如果开启走廊无敌，且当前处于 Corridor 阶段，则忽略失败触发
        if (DebugSettings.CorridorGodMode && GameManager.Instance != null && GameManager.Instance.IsInCorridor())
        {
            Debug.Log("[LightDetection] CorridorGodMode ON — 忽略失败触发。");
            return;
        }

        PlayerHide hideScript = playerObj.GetComponent<PlayerHide>();
        if (hideScript != null && hideScript.isHiding) return;

        hasFailed = true;

        if (audioSource != null) audioSource.Play();

        // 通过 AudioManager 停止 BGM（不再手动操作 MainCamera AudioSource）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM();
        }
        else
        {
            // 降级：直接停 MainCamera 上的 AudioSource
            GameObject mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamera != null)
            {
                AudioSource bgmSource = mainCamera.GetComponent<AudioSource>();
                if (bgmSource != null) bgmSource.Stop();
            }
        }

        playercontroller playerMove = playerObj.GetComponent<playercontroller>();
        if (playerMove != null) playerMove.enabled = false; 
        Animator playerAnim = playerObj.GetComponent<Animator>();
        if (playerAnim != null) playerAnim.enabled = false;

        StartCoroutine(BlackScreenAndRestart());
    }

IEnumerator BlackScreenAndRestart()
    {
        Debug.Log("🚨 保安抓捕触发！准备执行失败特效！");

        // 【修改】优先使用 FailEffectController 的抖动特效 + 重载
        if (FailEffectController.Instance != null)
        {
            // ⚠️ 不激活 blackScreenUI（DeathCanvas sortingOrder=999 > ModalLayer=90，
            //    黑布会遮住 fail 面板）。fail 面板本身负责全屏视觉反馈。
            FailEffectController.Instance.ShowFailEffect();
            yield break; // FailEffectController 内部处理重载
        }

        // ── 降级路径：无 FailEffectController 时走原来的黑屏逻辑 ──
        if (blackScreenUI == null)
        {
            Debug.LogError("❌ 'Black Screen UI' 槽位为空！");
        }
        else
        {
            blackScreenUI.SetActive(true);
        }

        yield return new WaitForSeconds(2.0f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadCurrentPhase();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}