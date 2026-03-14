#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SaveToolsEditor
{
    private const string DefaultFileName = "savegame.json";

    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, DefaultFileName);

    [MenuItem("Game/Save/Open Save Folder")]
    public static void OpenSaveFolder()
    {
        string folder = Application.persistentDataPath;
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        EditorUtility.RevealInFinder(folder);
    }

    [MenuItem("Game/Save/Delete Save File")]
    public static void DeleteSaveFile()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log($"[SaveToolsEditor] Deleted save file: {SavePath}");
        }
        else
        {
            Debug.Log("[SaveToolsEditor] No save file found.");
        }
    }

    [MenuItem("Game/Save/Delete Save File", true)]
    public static bool ValidateDeleteSaveFile()
    {
        return true;
    }
}
#endif