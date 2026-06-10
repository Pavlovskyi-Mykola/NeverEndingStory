#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(QuestDefinition))]
public class QuestDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var quest = (QuestDefinition)target;

        EditorGUILayout.HelpBox(
            "Use the Quest Editor window for the best editing experience.",
            MessageType.Info);

        if (GUILayout.Button("Open in Quest Editor", GUILayout.Height(28)))
            QuestEditorWindow.OpenQuest(quest);

        EditorGUILayout.Space(8);

        // Fallback: show raw fields so the asset is still readable in Inspector
        DrawDefaultInspector();
    }
}
#endif
