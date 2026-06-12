using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FlagDatabase))]
public class FlagDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var db = (FlagDatabase)target;

        int total = db.GetAllFlagIds().Count;
        EditorGUILayout.HelpBox(
            $"{total} flag ids currently available (generated from templates + custom).\n" +
            "Generate constants to reference them from code as WorldFlags.Npc.Bob.Met etc.",
            MessageType.Info);

        if (GUILayout.Button("Generate Constants (WorldFlags.Generated.cs)", GUILayout.Height(26)))
            WorldFlagConstantsGenerator.Generate();

        EditorGUILayout.Space(8);
        DrawDefaultInspector();
    }
}
