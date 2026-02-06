#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomPropertyDrawer(typeof(SpawnPointKeyAttribute))]
public class SpawnPointKeyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // one line by default, 2 lines when we show a hint
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // We need to find the sibling field "LocationScene" from the same schedule entry
        var locationSceneProp = FindSibling(property, "LocationScene");
        if (locationSceneProp == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Try to get LocationScene.SceneName (works only if your SceneReference has SceneName)
        // If your SceneReference uses a different field name, adjust GetSceneNameFromSceneReference below.
        string sceneName = GetSceneNameFromSceneReference(locationSceneProp);

        // If no scene chosen -> normal string
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Gather keys from open scenes
        var keys = CollectSpawnPointKeysFromOpenScenes(sceneName);

        if (keys.Count == 0)
        {
            // Fallback: manual entry + small hint
            EditorGUI.PropertyField(position, property, label);

            // Draw a subtle help box under (optional)
            // If you want the help box, uncomment and also increase GetPropertyHeight
            // var helpRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
            //     position.width, EditorGUIUtility.singleLineHeight * 1.2f);
            // EditorGUI.HelpBox(helpRect, $"Open scene '{sceneName}' to pick spawn keys.", MessageType.Info);

            return;
        }

        // Ensure a stable ordering
        keys.Sort(StringComparer.OrdinalIgnoreCase);

        // Add empty option (meaning "no preference")
        var options = new List<string> { "<none>" };
        options.AddRange(keys);

        int currentIndex = 0;
        var currentValue = property.stringValue;

        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            int found = options.FindIndex(s => string.Equals(s, currentValue, StringComparison.OrdinalIgnoreCase));
            currentIndex = found >= 0 ? found : 0;
        }

        EditorGUI.BeginProperty(position, label, property);

        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, options.ToArray());

        if (newIndex == 0)
            property.stringValue = ""; // none
        else
            property.stringValue = options[newIndex];

        EditorGUI.EndProperty();
    }

    private static SerializedProperty FindSibling(SerializedProperty property, string siblingName)
    {
        // property is SpawnPointKey
        // parent is the schedule entry
        // find sibling relative to parent
        var parentPath = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
        return property.serializedObject.FindProperty($"{parentPath}.{siblingName}");
    }

    private static string GetSceneNameFromSceneReference(SerializedProperty sceneRefProp)
    {
        // SceneReference has: [SerializeField] private Object sceneAsset;
        var assetProp = sceneRefProp.FindPropertyRelative("sceneAsset");
        if (assetProp == null) return "";

        var obj = assetProp.objectReferenceValue;
        return obj != null ? obj.name : "";
    }

    private static List<string> CollectSpawnPointKeysFromOpenScenes(string targetSceneName)
    {
        var result = new List<string>();

        // Check all currently open scenes in editor
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.IsValid() || !s.isLoaded) continue;
            if (!string.Equals(s.name, targetSceneName, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var root in s.GetRootGameObjects())
            {
                var spawners = root.GetComponentsInChildren<LocationNpcSpawner>(true);
                foreach (var spawner in spawners)
                {
                    if (spawner.SpawnPoints == null) continue;
                    foreach (var p in spawner.SpawnPoints)
                    {
                        if (p == null) continue;
                        if (string.IsNullOrWhiteSpace(p.Key)) continue;
                        result.Add(p.Key.Trim());
                    }
                }
            }
        }

        // Remove duplicates
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
#endif
