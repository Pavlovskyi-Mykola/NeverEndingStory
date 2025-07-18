using UnityEngine;

public class SceneLoaderButton : MonoBehaviour
{
    [SerializeField] private SceneType loadScene;

    public void LoadScene()
    {
        var _sceneDatabase = GameManager.Instance.sceneDatabase;
        SceneReference sceneRef = _sceneDatabase.GetScene(loadScene);
        GameManager.Instance.LoadScene(sceneRef);
    }
}
