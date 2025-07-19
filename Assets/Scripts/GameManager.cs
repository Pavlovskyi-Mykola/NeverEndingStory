using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event Action<SceneReference> OnSceneLoaded = delegate { };
    public event Action<SceneReference> OnSceneUnLoaded = delegate { };

    private List<AsyncOperation> _scenesToLoad = new();

    [Header("Scene Data")]
    public SceneDatabase sceneDatabase;

    //move this later
    public GameObject loadingInterface;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

    }

    public async void StartGame()
    {
        ShowLoadingScreen(true);

        LoadSceneAsync(sceneDatabase.TutorialArea);

        while (_scenesToLoad.Count > 0)
            await Task.Yield();

        UnloadSceneAsync(sceneDatabase.MainMenu);
        ShowLoadingScreen(false);
    }

    private void ShowLoadingScreen(bool OnOff)
    {
        loadingInterface.SetActive(OnOff);
    }

    public async void LoadSceneAsync(SceneReference sceneToLoad)
    {
        var asyncOperation = SceneManager.LoadSceneAsync(sceneToLoad.SceneName, LoadSceneMode.Additive);
        _scenesToLoad.Add(asyncOperation);
        while (!asyncOperation.isDone)
            await Task.Yield();

        _scenesToLoad.Remove(asyncOperation);
        OnSceneLoaded?.Invoke(sceneToLoad);
    }

    public async void UnloadSceneAsync(SceneReference sceneRef)
    {

        var asyncOperation = SceneManager.UnloadSceneAsync(sceneRef.SceneName);
        while (!asyncOperation.isDone)
            await Task.Yield();

        OnSceneUnLoaded?.Invoke(sceneRef);
    }

    public float TotalLoadingProgress
    {
        get
        {
            if (_scenesToLoad.Count == 0) return 1f;
            float total = 0f;
            foreach (var op in _scenesToLoad)
            {
                total += Mathf.Clamp01(op.progress / 0.9f); // Normalize
            }
            return total / _scenesToLoad.Count;
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }

}



