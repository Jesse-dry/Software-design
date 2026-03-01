using UnityEngine;
using System.Collections.Generic;

public class LevelJudge : MonoBehaviour
{
    public static LevelJudge Instance;

    private Dictionary<Vector2Int, PipeLogic> grid = new Dictionary<Vector2Int, PipeLogic>();
    
    // 【新增】记录游戏是否已经胜利
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
        // 如果已经赢了，再点水管就不做检测了
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
        // 如果已经赢了，水就不继续往下流了
        if (hasWon) return; 

        PipeLogic currentPipe = grid[currentPos];

        // 防止死循环
        if (currentPipe.isFilled) return;

        currentPipe.isFilled = true;

        // 【核心胜利逻辑】：如果这节管子是终点，游戏通关！
        if (currentPipe.isEndPoint)
        {
            hasWon = true;
            Debug.Log("🎉 恭喜通关！水流已经成功到达终点！🎉");
            return; 
        }

        // 继续往四周蔓延
        Vector2Int upPos = currentPos + Vector2Int.up;
        if (currentPipe.up && grid.ContainsKey(upPos) && grid[upPos].down)
        {
            FlowWater(upPos);
        }

        Vector2Int downPos = currentPos + Vector2Int.down;
        if (currentPipe.down && grid.ContainsKey(downPos) && grid[downPos].up)
        {
            FlowWater(downPos);
        }

        Vector2Int leftPos = currentPos + Vector2Int.left;
        if (currentPipe.left && grid.ContainsKey(leftPos) && grid[leftPos].right)
        {
            FlowWater(leftPos);
        }

        Vector2Int rightPos = currentPos + Vector2Int.right;
        if (currentPipe.right && grid.ContainsKey(rightPos) && grid[rightPos].left)
        {
            FlowWater(rightPos);
        }
    }
}