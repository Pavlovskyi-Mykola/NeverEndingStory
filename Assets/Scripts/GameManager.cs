using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }


    public event Action<string> OnSceneLoaded = delegate { };
    public event Action<string> OnSceneUnloaded = delegate { };

    [Header("Scene Data")]
    public SceneDatabase sceneDatabase;

    private SceneReference _currentActiveScene;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _currentActiveScene = sceneDatabase.MainMenu;
    }

    public void LoadScene (SceneReference sceneToLoad)
    {
        SceneManager.LoadScene(sceneToLoad.SceneName);
        //SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneToLoad.SceneName));

        OnSceneLoaded?.Invoke(sceneToLoad.SceneName);
    }
}



