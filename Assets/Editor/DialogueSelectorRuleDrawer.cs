#if UNITY_EDITOR
// This drawer is intentionally minimal.
// Edit DialogueRouteSet assets via Game > Dialogue > Route Set Editor.
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueRouteSet))]
public class DialogueRouteSetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Use the Route Set Editor window for a readable view of the dialogue flow.",
            MessageType.Info);

        if (GUILayout.Button("Open in Route Set Editor", GUILayout.Height(28)))
            DialogueRouteSetEditorWindow.OpenRouteSet((DialogueRouteSet)target);

        EditorGUILayout.Space(8);
        DrawDefaultInspector();
    }
}
#endif
