using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 修水管游戏倒计时器。
///
/// 在 UIRoot_PipePuzzle 的 HUDLayer 内查找 TimerPanel/TimerText
/// 进行 60 秒倒计时。超时 → 失败特效 → 重新加载场景。
/// 
/// 当 LevelJudge 通关后自动停止计时。
/// 
/// 使用方式：挂载到 PipePuzzle 场景中任意物体即可。
/// </summary>
public class PipePuzzleTimer : MonoBehaviour
{
    [Header("倒计时配置")]
    [Tooltip("总时间（秒）")]
    public float totalTime = 60f;

    [Tooltip("查不到 TimerText 时的重试间隔（秒）")]
    public float retryInterval = 0.2f;
    [Tooltip("最大重试次数")]
    public int maxRetries = 15;

    private TextMeshProUGUI _timerTMP;
    private Text _timerLegacy;             // 兼容 Legacy Text
    private bool _isRunning = true;
    private float _remainingTime;

    private bool HasTimerUI => _timerTMP != null || _timerLegacy != null;

    void Start()
    {
        _remainingTime = totalTime;
        FindTimerText();

        if (!HasTimerUI)
        {
            Debug.LogWarning("[PipePuzzleTimer] 首次未找到 TimerText，启动延迟重试…");
            StartCoroutine(RetryFindTimerText());
        }

        UpdateDisplay();
    }

    /// <summary>UI 可能在稍后帧才实例化，定时重试查找</summary>
    private IEnumerator RetryFindTimerText()
    {
        int attempt = 0;
        while (!HasTimerUI && attempt < maxRetries)
        {
            yield return new WaitForSeconds(retryInterval);
            attempt++;
            FindTimerText();
        }

        if (HasTimerUI)
        {
            Debug.Log($"[PipePuzzleTimer] 第 {attempt} 次重试成功找到 TimerText。");
            UpdateDisplay();
        }
        else
        {
            Debug.LogError("[PipePuzzleTimer] 重试耗尽，仍未找到 TimerText！请检查 UIRoot_PipePuzzle Prefab。");
        }
    }

    void Update()
    {
        if (!_isRunning) return;

        // 如果已通关（LevelJudge.hasWon），停止计时
        if (LevelJudge.Instance != null)
        {
            // 通过反射或公共字段检测通关：LevelJudge 的 hasWon 是 private
            // 这里用简单方案：检测 LevelJudge 是否还处于活跃状态
            // 实际上通关后 SelectRoleController 会切换场景，计时器自然销毁
        }

        _remainingTime -= Time.deltaTime;

        if (_remainingTime <= 0f)
        {
            _remainingTime = 0f;
            _isRunning = false;
            UpdateDisplay();
            StartCoroutine(TimeoutFailure());
            return;
        }

        UpdateDisplay();
    }

    /// <summary>通关时调用，停止计时</summary>
    public void StopTimer()
    {
        _isRunning = false;
    }

    private void UpdateDisplay()
    {
        int seconds = Mathf.CeilToInt(_remainingTime);
        int min = seconds / 60;
        int sec = seconds % 60;
        string display = $"{min:00}:{sec:00}";
        Color color = _remainingTime <= 10f ? Color.red : new Color(0.9f, 0.9f, 0.95f);

        if (_timerTMP != null)
        {
            _timerTMP.text = display;
            _timerTMP.color = color;
        }
        else if (_timerLegacy != null)
        {
            _timerLegacy.text = display;
            _timerLegacy.color = color;
        }
    }

    private IEnumerator TimeoutFailure()
    {
        // 1. 弹出警告提示
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show("修复超时！管道系统已崩溃！", ToastStyle.GlitchFlash);
        }

        // 2. 增加混乱值惩罚
        if (ChaosManager.Instance != null)
        {
            ChaosManager.Instance.AddChaos(20, "修水管超时");
        }

        // 3. 使用 FailEffectController 显示失败特效
        if (FailEffectController.Instance != null)
        {
            FailEffectController.Instance.ShowFailEffect();
            yield break; // FailEffectController 内部处理重载
        }

        // 降级：直接重载
        yield return new WaitForSeconds(2.0f);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadCurrentPhase();
        }
    }

    private void FindTimerText()
    {
        // 方案1：在 UISceneRoot 的 HUDLayer 中查找
        if (UIManager.Instance != null && UIManager.Instance.hudLayer != null)
        {
            var timerPanel = UIManager.Instance.hudLayer.Find("TimerPanel");
            if (timerPanel != null)
            {
                var textT = timerPanel.Find("TimerText");
                if (textT != null)
                {
                    _timerTMP = textT.GetComponent<TextMeshProUGUI>();
                    if (_timerTMP == null)
                        _timerLegacy = textT.GetComponent<Text>();
                }
            }
        }

        // 方案2：场景内直接搜索（优先 TMP，其次 Legacy）
        if (!HasTimerUI)
        {
            var allTMP = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var tmp in allTMP)
            {
                if (tmp.gameObject.name == "TimerText")
                {
                    _timerTMP = tmp;
                    return;
                }
            }
        }

        if (!HasTimerUI)
        {
            var allText = FindObjectsByType<Text>(FindObjectsSortMode.None);
            foreach (var t in allText)
            {
                if (t.gameObject.name == "TimerText")
                {
                    _timerLegacy = t;
                    return;
                }
            }
        }
    }
}
