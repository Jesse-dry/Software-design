using UnityEngine;

public class ExplorationSystem : MonoBehaviour, IGameSystem
{
    public void OnEnter(GameState state)
    {
        if (state == GameState.Exploration)
        {
            Debug.Log("ExplorationSystem Enter");
        }
    }

    public void OnExit(GameState state)
    {
        
    }
}