using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class QuestIdEditorUtility
{
    private static string[] _cachedIds;
    private static Dictionary<string, string> _titleById;
    private static double _lastRefreshTime;

    private static void RefreshIfStale()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_cachedIds != null && now - _lastRefreshTime < 1.0d)
            return;

        _lastRefreshTime = now;

        var ids = new List<string>();
        _titleById = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (def == null || string.IsNullOrWhiteSpace(def.QuestId))
                continue;

            ids.Add(def.QuestId);
            _titleById[def.QuestId] = string.IsNullOrWhiteSpace(def.Title) ? def.QuestId : def.Title;
        }

        _cachedIds = ids
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
    }

    public static string[] GetAllQuestIds()
    {
        RefreshIfStale();
        return _cachedIds;
    }

    public static bool IsKnownQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return false;
        RefreshIfStale();
        return Array.IndexOf(_cachedIds, questId) >= 0;
    }

    public static string GetTitle(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return null;
        RefreshIfStale();
        return _titleById != null && _titleById.TryGetValue(questId, out var t) ? t : null;
    }

    public static void DrawQuestIdField(Rect rect, SerializedProperty stringProp, GUIContent label)
    {
        if (stringProp == null)
        {
            EditorGUI.LabelField(rect, label != null ? label.text : "", "Missing string property");
            return;
        }

        string current = stringProp.stringValue;
        bool isKnown = IsKnownQuest(current);

        float buttonWidth = 26f;
        float spacing = 4f;
        Rect fieldRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - spacing, rect.height);
        Rect buttonRect = new Rect(fieldRect.xMax + spacing, rect.y, buttonWidth, rect.height);

        Color oldColor = GUI.backgroundColor;

        if (!string.IsNullOrEmpty(current) && !isKnown)
            GUI.backgroundColor = new Color(1f, 0.6f, 0.55f); // unknown quest -> red-ish

        if (label != null)
            stringProp.stringValue = EditorGUI.TextField(fieldRect, label, current);
        else
            stringProp.stringValue = EditorGUI.TextField(fieldRect, current);

        GUI.backgroundColor = oldColor;

        if (GUI.Button(buttonRect, "⋯"))
            QuestPickerDropdown.Show(buttonRect, stringProp);
    }
}
