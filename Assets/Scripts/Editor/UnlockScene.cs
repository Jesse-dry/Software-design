using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class UnlockScene
{
    // 这会在 Unity 顶部菜单栏变出一个神奇的按钮
    [MenuItem("Tools/一键解除强制场景锁定")]
    public static void Unlock()
    {
        // 这一句就是用来砸烂那个强制锁的！
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("✅ 强制锁定已解除！现在点播放，就会直接运行当前看着的场景了！");
    }
}