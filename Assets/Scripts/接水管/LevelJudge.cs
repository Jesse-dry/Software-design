using UnityEngine;
using System.Collections; // 【必须添加】用来支持延迟转场的协程
using System.Collections.Generic;

public class LevelJudge : MonoBehaviour
{
    public static LevelJudge Instance;

    private Dictionary<Vector2Int, PipeLogic> grid = new Dictionary<Vector2Int, PipeLogic>();

    private bool hasWon = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CheckPath();
    }

    public void CheckPath()
    {
        if (hasWon) return;

        grid.Clear();
        PipeLogic[] allPipes = FindObjectsByType<PipeLogic>(FindObjectsSortMode.None);

        Vector2Int? startPos = null;

        foreach (var pipe in allPipes)
        {
            pipe.isFilled = false;

            Vector2Int pos = new Vector2Int(
                Mathf.RoundToInt(pipe.transform.position.x),
                Mathf.RoundToInt(pipe.transform.position.y)
            );

            if (!grid.ContainsKey(pos))
            {
                grid.Add(pos, pipe);
            }

            if (pipe.isStartPoint)
            {
                startPos = pos;
            }
        }

        if (startPos.HasValue)
        {
            FlowWater(startPos.Value);
        }
    }

    void FlowWater(Vector2Int currentPos)
    {
        if (hasWon) return;

        PipeLogic currentPipe = grid[currentPos];

        if (currentPipe.isFilled) return;

        currentPipe.isFilled = true;

        // 【核心改造】：判定胜利后，触发延迟转场协程
        if (currentPipe.isEndPoint)
        {
            hasWon = true;
            Debug.Log("🎉 恭喜通关！水流已经成功到达终点！🎉");

            // 启动华丽的胜利结算流！
            StartCoroutine(VictoryAndTransition());
            return;
        }

        // 继续往四周蔓延
        Vector2Int upPos = currentPos + Vector2Int.up;
        if (currentPipe.up && grid.ContainsKey(upPos) && grid[upPos].down) FlowWater(upPos);

        Vector2Int downPos = currentPos + Vector2Int.down;
        if (currentPipe.down && grid.ContainsKey(downPos) && grid[downPos].up) FlowWater(downPos);

        Vector2Int leftPos = currentPos + Vector2Int.left;
        if (currentPipe.left && grid.ContainsKey(leftPos) && grid[leftPos].right) FlowWater(leftPos);

        Vector2Int rightPos = currentPos + Vector2Int.right;
        if (currentPipe.right && grid.ContainsKey(rightPos) && grid[rightPos].left) FlowWater(rightPos);
    }

    // ==========================================
    // 【新增】胜利转场与证据发放逻辑
    // ==========================================
    private IEnumerator VictoryAndTransition()
    {
        // 0. 停止倒计时器
        var timer = FindAnyObjectByType<PipePuzzleTimer>();
        if (timer != null) timer.StopTimer();

        // 1. 弹出胜利提示
        if (UIManager.Instance?.Toast != null)
            UIManager.Instance.Toast.Show("管线修复完毕！水压恢复正常！", colorType: ToastColor.Positive);

        // 2. 停顿 2 秒
        yield return new WaitForSeconds(2.0f);

        // 3. 结算阿卡那牌 — 星币
        if (AkanaManager.Instance != null)
            AkanaManager.Instance.CollectCard(AkanaCardId.星币);

        // 4. Toast 提示卡牌获得
        if (UIManager.Instance?.Toast != null)
            UIManager.Instance.Toast.Show("获得了【星币】阿卡那牌！", colorType: ToastColor.Positive);

        yield return new WaitForSeconds(1.0f);

        // 5. 询问是否查看星币牌说明 → 关闭后返回走廊选角色
        AkanaVictoryHelper.AskViewCard(
            AkanaCardId.星币,
            "恭喜获得【星币】阿卡那牌，是否查看牌面内容？",
            onFinished: () =>
            {
                Debug.Log("[LevelJudge] 接水管通关，返回 Corridor SelectRole！");
                SelectRoleController.ReturnToCorridorSelectRole();
            }
        );
    }

    /// <summary>
    /// 【预留接口】：在这里对接未来的卡牌/背包系统
    /// </summary>
    private void GrantEvidenceCards(int count)
    {
        // 已由 AkanaManager.CollectCard 替代
        Debug.Log($"[系统提示] 成功获取了 {count} 张证据牌！");
    }
}