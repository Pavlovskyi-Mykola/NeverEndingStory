#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueGraph))]
public class DialogueGraphAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var graph = (DialogueGraph)target;

        EditorGUILayout.HelpBox(
            "Use the Dialogue Editor window to edit nodes and connections.",
            MessageType.Info);

        if (GUILayout.Button("Open in Dialogue Editor", GUILayout.Height(28)))
            DialogueGraphEditorWindow.OpenGraph(graph);

        EditorGUILayout.Space(8);

        // Fallback: raw fields stay readable/editable in the Inspector
        // (start node id, CountsAsRelationshipTalk, ...).
        DrawDefaultInspector();
    }
}
#endif
