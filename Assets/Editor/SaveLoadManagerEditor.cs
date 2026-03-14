#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SaveLoadManager))]
public class SaveLoadManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        var mgr = (SaveLoadManager)target;
        if (mgr == null) return;

        EditorGUILayout.LabelField("Save File", mgr.SavePath);

        GUILayout.Space(6);

        if (GUILayout.Button("Save Now"))
            mgr.SaveGame();

        if (GUILayout.Button("Load Now"))
            mgr.LoadGameContextMenu();

        GUI.color = Color.red;
        if (GUILayout.Button("Delete Save File"))
            mgr.DeleteSaveFile();
        GUI.color = Color.white;
    }
}
#endif
