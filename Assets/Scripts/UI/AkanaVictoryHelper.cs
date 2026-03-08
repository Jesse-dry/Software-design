using UnityEngine;

/// <summary>
/// 通关获得阿卡那牌后的统一 UI 流程工具。
///
/// 流程：Modal 询问"是否查看阿卡那牌？"
///   → 是：弹出对应的卡牌说明面板（ModalLayer 下的 权杖牌/星币牌/宝剑牌/圣杯牌），
///         关闭面板后执行 onFinished
///   → 否：直接执行 onFinished
///
/// 各场景通关脚本只需一行调用：
///   AkanaVictoryHelper.AskViewCard(AkanaCardId.权杖, promptText, onFinished: DoNext);
/// </summary>
public static class AkanaVictoryHelper
{
    /// <summary>
    /// 弹出 Modal 询问玩家是否查看刚获得的阿卡那牌。
    /// </summary>
    /// <param name="cardId">刚收集到的卡牌 ID</param>
    /// <param name="promptText">Modal 显示的文案</param>
    /// <param name="onFinished">无论查看与否，最终都会执行的回调</param>
    public static void AskViewCard(AkanaCardId cardId, string promptText, System.Action onFinished)
    {
        if (UIManager.Instance != null && UIManager.Instance.Modal != null)
        {
            UIManager.Instance.Modal.ShowConfirm(
                promptText,
                onYes: () => ShowCardPanel(cardId, onFinished),
                onNo:  () =>
                {
                    Debug.Log($"[AkanaVictory] 玩家跳过查看 {cardId}");
                    onFinished?.Invoke();
                }
            );
        }
        else
        {
            Debug.LogWarning("[AkanaVictory] Modal 不可用，直接执行 onFinished。");
            onFinished?.Invoke();
        }
    }

    /// <summary>
    /// 显示卡牌说明面板，关闭后触发回调。
    /// </summary>
    private static void ShowCardPanel(AkanaCardId cardId, System.Action onFinished)
    {
        var hud = AkanaHUDController.Instance;
        if (hud != null)
        {
            Debug.Log($"[AkanaVictory] 显示卡牌说明: {AkanaManager.GetCardPanelName(cardId)}");
            hud.ShowCardDetailWithCallback(cardId, onFinished);
        }
        else
        {
            Debug.LogWarning("[AkanaVictory] AkanaHUDController 不存在，直接执行 onFinished。");
            onFinished?.Invoke();
        }
    }
}
