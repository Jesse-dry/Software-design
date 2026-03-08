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
        // 1. 弹出胜利提示（绿色文字正面反馈）
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show("管线修复完毕！水压恢复正常！", colorType: ToastColor.Positive);
        }

        // 2. 停顿 2 秒，让玩家看看完整连通的水管，享受一下解密的快感
        yield return new WaitForSeconds(2.0f);

        // 3. 结算证据牌（之前配置里清洁工路线是 1 张牌）
        GrantEvidenceCards(1);

        yield return new WaitForSeconds(1.0f); // 再稍微停顿一下让玩家看清拿到牌了

        // 4. 呼叫大管家，雷霆切入法庭！
        if (GameManager.Instance != null)
        {
            Debug.Log("[LevelJudge] 准备完毕，进入 Court（法庭）场景！");
            GameManager.Instance.EnterPhase(GamePhase.Court);
        }
    }

    /// <summary>
    /// 【预留接口】：在这里对接未来的卡牌/背包系统
    /// </summary>
    private void GrantEvidenceCards(int count)
    {
        // TODO: 等你的背包系统写好后，把增加卡牌的代码填在这里，例如：
        // InventoryManager.Instance.AddCards(count);

        Debug.Log($"[系统提示] 成功获取了 {count} 张证据牌！（代码占位）");

        // 用 Toast 飘字给玩家视觉奖励
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show($"成功获取 {count} 张线索牌！", colorType: ToastColor.Positive);
        }
    }
}