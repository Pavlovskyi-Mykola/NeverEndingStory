using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private SceneDatabase sceneDatabase;

    [Header("Startup")]
    [SerializeField] private bool loadUIOnStart = true;
    [SerializeField] private bool loadMainMenuOnStart = true;
    public string CurrentLocation { get; private set; }

    // Track loaded scenes by name
    private readonly HashSet<string> _loaded = new HashSet<string>();

    public event Action<string> SceneLoadStarted;
    public event Action<string> SceneLoadCompleted;
    public event Action<string> SceneUnloadStarted;
    public event Action<string> SceneUnloadCompleted;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CacheAlreadyLoadedScenes();
    }

    private async void Start()
    {
        // Start() runs after Awake() and after first scene is loaded.
        if (loadUIOnStart && sceneDatabase != null && sceneDatabase.UI != null && sceneDatabase.UI.IsValid)
            await Load(sceneDatabase.UI, setActive: false);

        if (loadMainMenuOnStart && sceneDatabase != null && sceneDatabase.MainMenu != null && sceneDatabase.MainMenu.IsValid)
            await Load(sceneDatabase.MainMenu, setActive: true);
            CurrentLocation = sceneDatabase.MainMenu.SceneName;
    }

    private void CacheAlreadyLoadedScenes()
    {
        _loaded.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (sc.IsValid() && sc.isLoaded && !string.IsNullOrEmpty(sc.name))
                _loaded.Add(sc.name);
        }
    }

    public SceneDatabase Scenes => sceneDatabase;

    public bool IsLoaded(SceneReference sceneRef) =>
        sceneRef != null && sceneRef.IsValid && _loaded.Contains(sceneRef.SceneName);

    public Task Load(SceneReference sceneRef, bool setActive = false) =>
        Load(sceneRef?.SceneName, setActive);

    public Task Unload(SceneReference sceneRef) =>
        Unload(sceneRef?.SceneName);

    public async Task Load(string sceneName, bool setActive = false)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Load failed: sceneName is null/empty.");
            return;
        }

        if (_loaded.Contains(sceneName))
        {
            // Already loaded; optionally set active
            if (setActive)
                SetActive(sceneName);
            return;
        }

        SceneLoadStarted?.Invoke(sceneName);

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"Load failed: LoadSceneAsync returned null for '{sceneName}'. Is it in Build Settings?");
            return;
        }

        op.allowSceneActivation = true;

        // Wait until done
        while (!op.isDone)
            await Task.Yield();

        _loaded.Add(sceneName);

        if (setActive)
            SetActive(sceneName);

        SceneLoadCompleted?.Invoke(sceneName);
    }

    public async Task Unload(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Unload failed: sceneName is null/empty.");
            return;
        }

        if (!_loaded.Contains(sceneName))
            return;

        SceneUnloadStarted?.Invoke(sceneName);

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            _loaded.Remove(sceneName);
            SceneUnloadCompleted?.Invoke(sceneName);
            return;
        }

        // Don’t let Unity unload the active scene without switching away first
        if (SceneManager.GetActiveScene().name == sceneName)
        {
            // Pick any other loaded scene as fallback (or your bootstrap)
            foreach (var s in _loaded)
            {
                if (s != sceneName)
                {
                    SetActive(s);
                    break;
                }
            }
        }

        var op = SceneManager.UnloadSceneAsync(scene);
        if (op == null)
        {
            Debug.LogError($"Unload failed: UnloadSceneAsync returned null for '{sceneName}'.");
            return;
        }

        while (!op.isDone)
            await Task.Yield();

        _loaded.Remove(sceneName);

        SceneUnloadCompleted?.Invoke(sceneName);
    }

    public bool SetActive(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        var sc = SceneManager.GetSceneByName(sceneName);
        if (!sc.IsValid() || !sc.isLoaded)
            return false;

        SceneManager.SetActiveScene(sc);
        return true;
    }

    // Optional helper: load one, unload others (simple "switch")
    public async Task SwitchTo(SceneReference target, params SceneReference[] scenesToUnload)
    {
        if (target == null || !target.IsValid)
        {
            Debug.LogError("SwitchTo failed: target is invalid.");
            return;
        }

        await Load(target, setActive: true);

        if (scenesToUnload == null) return;
        foreach (var s in scenesToUnload)
        {
            if (s != null && s.IsValid && s.SceneName != target.SceneName)
                await Unload(s);
        }
    }

    public async Task SwitchLocation(SceneReference targetLocation)
    {
        if (targetLocation == null || !targetLocation.IsValid)
            return;

        // unload previous location if any
        if (!string.IsNullOrEmpty(CurrentLocation))
            await Unload(CurrentLocation);

        await Load(targetLocation, setActive: true);
        CurrentLocation = targetLocation.SceneName;
    }
}
