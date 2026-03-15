#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SaveLoadManager))]
public class SaveLoadManagerEditor : Editor
{
    private string _slotId = "slot_1";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        var mgr = (SaveLoadManager)target;
        if (mgr == null) return;

        _slotId = EditorGUILayout.TextField("Slot Id", string.IsNullOrWhiteSpace(_slotId) ? mgr.ActiveSlotId : _slotId);

        GUILayout.Space(6);

        if (GUILayout.Button("Use Slot"))
            mgr.SetActiveSlot(_slotId);

        if (GUILayout.Button("Save Slot"))
            mgr.SaveGame(_slotId, "manual");

        if (GUILayout.Button("Load Slot"))
            _ = mgr.LoadGame(_slotId);

        if (GUILayout.Button("Delete Slot"))
            mgr.DeleteSaveFile(_slotId);

        GUI.color = Color.yellow;
        if (GUILayout.Button("Clear Runtime + Delete Active Slot"))
            mgr.ClearCurrentSlotAndRuntimeState();
        GUI.color = Color.white;

        GUILayout.Space(8);

        var info = mgr.GetSlotInfo(_slotId);
        EditorGUILayout.LabelField("Exists", info.exists.ToString());
        EditorGUILayout.LabelField("Version", info.version.ToString());
        EditorGUILayout.LabelField("Saved At", info.savedAtUtc ?? "-");
        EditorGUILayout.LabelField("Location", info.currentLocationSceneName ?? "-");
        EditorGUILayout.LabelField("Tracked Quest", info.trackedQuestId ?? "-");
        EditorGUILayout.LabelField("Path", info.filePath ?? "-");
    }
}
#endif
