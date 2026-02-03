using UnityEngine;

public interface IGameSystem
{
    void OnEnter(GameState state);
    void OnExit(GameState state);
}
