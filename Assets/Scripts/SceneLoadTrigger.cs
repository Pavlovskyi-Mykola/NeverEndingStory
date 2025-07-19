using UnityEngine;

public class SceneTrigger : MonoBehaviour
{
    [SerializeField] private SceneReference sceneToLoad;
    [SerializeField] private bool unloadOnExit = true;

    private bool _isLoaded;


    private void OnTriggerEnter(Collider other)
    {
        if (!_isLoaded && other.CompareTag("Player"))
        {
            GameManager.Instance.LoadSceneAsync(sceneToLoad);
            _isLoaded = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_isLoaded && unloadOnExit && other.CompareTag("Player"))
        {
            GameManager.Instance.UnloadSceneAsync(sceneToLoad);
            _isLoaded = false;
        }
    }
}
