using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WorldFlagEditorUtility
{
    private static string[] _cachedFlags;
    private static double _lastRefreshTime;

    public static string[] GetAllFlags()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_cachedFlags != null && now - _lastRefreshTime < 1.0d)
            return _cachedFlags;

        _lastRefreshTime = now;

        var db = FlagDatabase.Instance;
        if (db == null)
        {
            _cachedFlags = Array.Empty<string>();
            return _cachedFlags;
        }

        _cachedFlags = db.GetAllFlagIds()
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        return _cachedFlags;
    }

    public static int GetIndexOf(string currentValue, string[] options)
    {
        if (options == null || options.Length == 0) return -1;
        if (string.IsNullOrEmpty(currentValue)) return -1;

        for (int i = 0; i < options.Length; i++)
        {
            if (string.Equals(options[i], currentValue, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    public static bool IsKnownFlag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var all = GetAllFlags();
        return Array.IndexOf(all, value) >= 0;
    }

    public static string GetShortLabel(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return "<None>";

        return flagId;
    }

    /// <summary>Returns the FlagDatabase asset, offering to create one when none exists yet.</summary>
    public static FlagDatabase GetOrCreateDatabase()
    {
        var db = FlagDatabase.Instance;
        if (db != null) return db;

        if (!EditorUtility.DisplayDialog("Flag Database missing",
                "No FlagDatabase asset exists yet. Create one at Assets/ScriptableObjects/FlagDatabase.asset?",
                "Create", "Cancel"))
            return null;

        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        db = ScriptableObject.CreateInstance<FlagDatabase>();
        db.SeedDefaultTemplates();
        AssetDatabase.CreateAsset(db, "Assets/ScriptableObjects/FlagDatabase.asset");
        AssetDatabase.SaveAssets();
        return db;
    }

    /// <summary>Adds a flag id to the FlagDatabase (creating the asset if needed). Returns true when the id is usable afterwards.</summary>
    public static bool AddFlagToDatabase(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return false;

        var db = GetOrCreateDatabase();
        if (db == null)
            return false;

        if (db.IsKnown(flagId.Trim()))
            return true; // already there — still fine to assign

        Undo.RecordObject(db, "Add World Flag");

        if (!db.AddFlagEditor(flagId))
            return false;

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssetIfDirty(db);
        _cachedFlags = null; // force picker refresh
        return true;
    }

    public static void DrawFlagField(Rect rect, SerializedProperty stringProp, GUIContent label)
    {
        if (stringProp == null)
        {
            EditorGUI.LabelField(rect, label.text, "Missing string property");
            return;
        }

        string current = stringProp.stringValue;
        bool isKnown = IsKnownFlag(current);

        float buttonWidth = 26f;
        float spacing = 4f;
        Rect fieldRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - spacing, rect.height);
        Rect buttonRect = new Rect(fieldRect.xMax + spacing, rect.y, buttonWidth, rect.height);

        Color oldColor = GUI.backgroundColor;

        if (!string.IsNullOrEmpty(current) && !isKnown)
            GUI.backgroundColor = new Color(1f, 0.92f, 0.45f);

        stringProp.stringValue = EditorGUI.TextField(fieldRect, label, current);

        GUI.backgroundColor = oldColor;

        if (GUI.Button(buttonRect, "⋯"))
        {
            FlagPickerDropdown.Show(buttonRect, stringProp);
        }
    }
}
