using UnityEngine;

public class SceneButton : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private SceneReference targetScene;

    [Header("Optional unload")]
    [SerializeField] private SceneReference[] scenesToUnload;

    public void LoadScene()
    {
        if (targetScene == null || !targetScene.IsValid)
        {
            Debug.LogError($"{name}: Target scene is not set.");
            return;
        }

        _ = GameManager.Instance.SwitchTo(
            targetScene,
            scenesToUnload
        );
    }
}