using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        var flags = new List<string>();
        CollectConstStringsRecursive(typeof(WorldFlags), flags);

        _cachedFlags = flags
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        return _cachedFlags;
    }

    private static void CollectConstStringsRecursive(Type type, List<string> results)
    {
        if (type == null) return;

        const BindingFlags bindingFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        foreach (var field in type.GetFields(bindingFlags))
        {
            if (field.FieldType != typeof(string)) continue;
            if (!field.IsLiteral || field.IsInitOnly) continue;

            object raw = field.GetRawConstantValue();
            if (raw is string s && !string.IsNullOrWhiteSpace(s))
                results.Add(s);
        }

        foreach (var nested in type.GetNestedTypes(bindingFlags))
            CollectConstStringsRecursive(nested, results);
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