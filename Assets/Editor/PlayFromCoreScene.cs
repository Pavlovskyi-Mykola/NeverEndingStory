#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayFromCoreScene
{
    private const string PrefKey = "NES_PlayFromCoreScene_Enabled";
    private const string CoreScenePath = "Assets/Scenes/CoreLogic.unity";
    // change this path to your real scene path

    static PlayFromCoreScene()
    {
        ApplySetting();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Play From Core Scene/Enabled")]
    private static void Toggle()
    {
        bool enabled = !EditorPrefs.GetBool(PrefKey, true);
        EditorPrefs.SetBool(PrefKey, enabled);
        ApplySetting();
    }

    [MenuItem("Tools/Play From Core Scene/Enabled", true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked("Tools/Play From Core Scene/Enabled", EditorPrefs.GetBool(PrefKey, true));
        return true;
    }

    [MenuItem("Tools/Play From Core Scene/Play _F9")]
    private static void PlayFromCore()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath);
        if (sceneAsset == null)
        {
            Debug.LogError($"Core scene not found at path: {CoreScenePath}");
            return;
        }

        EditorSceneManager.playModeStartScene = sceneAsset;
        EditorApplication.isPlaying = true;
    }

    private static void ApplySetting()
    {
        bool enabled = EditorPrefs.GetBool(PrefKey, true);

        if (!enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath);
        EditorSceneManager.playModeStartScene = sceneAsset;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Optional: keep the setting persistent after exiting play mode too
        if (state == PlayModeStateChange.EnteredEditMode)
            ApplySetting();
    }
}
#endif