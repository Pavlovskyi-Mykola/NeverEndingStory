#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor half of InactiveOnLoad: sweeps open scenes on save and on entering play
/// mode, disabling (and logging) any marked object that was left active. Keeps
/// saved scenes correct so the runtime guard in InactiveOnLoad.Awake stays silent.
/// </summary>
[InitializeOnLoad]
public static class InactiveOnLoadEnforcer
{
    static InactiveOnLoadEnforcer()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorSceneManager.sceneSaving += HandleSceneSaving;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        // Before play-mode serialization, so play starts with the corrected state
        // and the fix persists in the edit-mode scene afterwards.
        if (change != PlayModeStateChange.ExitingEditMode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
            Enforce(SceneManager.GetSceneAt(i));
    }

    private static void HandleSceneSaving(Scene scene, string path) => Enforce(scene);

    [MenuItem("Tools/UI/Disable 'Inactive On Load' objects in open scenes")]
    private static void EnforceOpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Enforce(SceneManager.GetSceneAt(i));
    }

    private static void Enforce(Scene scene)
    {
        if (!scene.isLoaded)
            return;

        foreach (var root in scene.GetRootGameObjects())
        {
            // Include inactive: a marked object under an inactive parent can still
            // be saved activeSelf=true and would pop on when the parent enables.
            foreach (var marker in root.GetComponentsInChildren<InactiveOnLoad>(true))
            {
                if (!marker.gameObject.activeSelf)
                    continue;

                Undo.RecordObject(marker.gameObject, "Disable InactiveOnLoad object");
                marker.gameObject.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);

                Debug.Log($"[InactiveOnLoad] Disabled '{Path(marker.transform)}' — it was left on in '{scene.name}'.", marker.gameObject);
            }
        }
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
#endif
