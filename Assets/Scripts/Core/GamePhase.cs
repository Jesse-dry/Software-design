using UnityEngine;

public enum GamePhase
{
    Boot,   //启动：加载资源，初始化系统
    Abyss,  //潜渊：核心玩法阶段，玩家操作、探索
    Court,  //庭审：剧情/结算阶段，展示结果
    Result, //裁决：最终结算
}
