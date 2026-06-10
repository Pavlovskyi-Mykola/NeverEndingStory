#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogueSelectorRule))]
public class DialogueSelectorRuleDrawer : PropertyDrawer
{
    // Foldout per-property (per rule element) using propertyPath key
    private static readonly Dictionary<string, bool> _locFoldouts = new();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = 0f;
        float pad = 2f;

        var kindProp = property.FindPropertyRelative("kind");
        var graphProp = property.FindPropertyRelative("graph");
        var poolProp = property.FindPropertyRelative("pool");
        var pickModeProp = property.FindPropertyRelative("pickMode");
        var allowedPhasesProp = property.FindPropertyRelative("allowedPhases");
        var requireNotSeenDialogueProp = property.FindPropertyRelative("requireNotSeenDialogue");
        var requireNotSeenThisProp = property.FindPropertyRelative("requireNotSeenThis");
        var allowedDaysProp = property.FindPropertyRelative("allowedDays");
        var allowedLocIdsProp = property.FindPropertyRelative("allowedLocationIds");

        // kind
        h += EditorGUI.GetPropertyHeight(kindProp, true) + pad;

        var kind = (DialogueRuleKind)kindProp.enumValueIndex;

        // graph/pool section
        if (kind == DialogueRuleKind.RepeatablePool)
        {
            h += EditorGUI.GetPropertyHeight(poolProp, true) + pad;      // important: array height
            h += EditorGUI.GetPropertyHeight(pickModeProp, true) + pad;
        }
        else
        {
            h += EditorGUI.GetPropertyHeight(graphProp, true) + pad;
        }

        // conditions
        h += EditorGUI.GetPropertyHeight(allowedPhasesProp, true) + pad;
        h += EditorGUI.GetPropertyHeight(allowedDaysProp, true) + pad;
        h += EditorGUI.GetPropertyHeight(requireNotSeenDialogueProp, true) + pad;
        h += EditorGUI.GetPropertyHeight(requireNotSeenThisProp, true) + pad;

        // locations foldout line
        h += EditorGUIUtility.singleLineHeight + pad;

        // expanded location list height
        bool fold = GetFoldout(property);
        if (fold)
        {
            var db = SceneDatabase.Instance;
            var ids = GetLocationIds(db);

            if (db == null || ids.Length == 0)
            {
                // helpbox ~2 lines
                h += EditorGUIUtility.singleLineHeight * 2f + pad;
            }
            else
            {
                // buttons row
                h += EditorGUIUtility.singleLineHeight + pad;

                // checkbox rows (cap to avoid huge inspectors)
                int shown = Mathf.Min(ids.Length, 20);
                h += shown * (EditorGUIUtility.singleLineHeight + pad);

                if (ids.Length > shown)
                    h += EditorGUIUtility.singleLineHeight + pad;
            }
        }

        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        float pad = 2f;

        Rect NextRect(float height)
        {
            var r = new Rect(position.x, y, position.width, height);
            y += height + pad;
            return r;
        }

        var kindProp = property.FindPropertyRelative("kind");
        var graphProp = property.FindPropertyRelative("graph");
        var poolProp = property.FindPropertyRelative("pool");
        var pickModeProp = property.FindPropertyRelative("pickMode");
        var allowedPhasesProp = property.FindPropertyRelative("allowedPhases");
        var requireNotSeenDialogueProp = property.FindPropertyRelative("requireNotSeenDialogue");
        var requireNotSeenThisProp = property.FindPropertyRelative("requireNotSeenThis");
        var allowedDaysProp = property.FindPropertyRelative("allowedDays");
        var allowedLocIdsProp = property.FindPropertyRelative("allowedLocationIds");

        // kind
        {
            float h = EditorGUI.GetPropertyHeight(kindProp, true);
            EditorGUI.PropertyField(NextRect(h), kindProp, true);
        }

        var kind = (DialogueRuleKind)kindProp.enumValueIndex;

        // graph/pool
        if (kind == DialogueRuleKind.RepeatablePool)
        {
            float hPool = EditorGUI.GetPropertyHeight(poolProp, true);
            EditorGUI.PropertyField(NextRect(hPool), poolProp, new GUIContent("Pool"), true);

            float hPick = EditorGUI.GetPropertyHeight(pickModeProp, true);
            EditorGUI.PropertyField(NextRect(hPick), pickModeProp, true);
        }
        else
        {
            float hGraph = EditorGUI.GetPropertyHeight(graphProp, true);
            EditorGUI.PropertyField(NextRect(hGraph), graphProp, new GUIContent("Graph"), true);
        }

        // conditions
        {
            float h = EditorGUI.GetPropertyHeight(allowedPhasesProp, true);
            EditorGUI.PropertyField(NextRect(h), allowedPhasesProp, true);
        }
        {
            float h = EditorGUI.GetPropertyHeight(allowedDaysProp, true);
            EditorGUI.PropertyField(NextRect(h), allowedDaysProp, true);
        }
        {
            float h = EditorGUI.GetPropertyHeight(requireNotSeenDialogueProp, true);
            EditorGUI.PropertyField(NextRect(h), requireNotSeenDialogueProp, true);
        }
        {
            float h = EditorGUI.GetPropertyHeight(requireNotSeenThisProp, true);
            EditorGUI.PropertyField(NextRect(h), requireNotSeenThisProp, true);
        }

        // locations foldout
        {
            bool fold = GetFoldout(property);
            var r = NextRect(EditorGUIUtility.singleLineHeight);
            bool next = EditorGUI.Foldout(r, fold, "Allowed Locations (empty = everywhere)", true);
            SetFoldout(property, next);

            if (next)
                y = DrawLocationMultiSelect(y, position, pad, allowedLocIdsProp);
        }

        EditorGUI.EndProperty();
    }

    private float DrawLocationMultiSelect(float y, Rect full, float pad, SerializedProperty allowedLocIdsProp)
    {
        Rect Next(float height)
        {
            var r = new Rect(full.x, y, full.width, height);
            y += height + pad;
            return r;
        }

        var db = SceneDatabase.Instance;
        var ids = GetLocationIds(db);

        if (db == null)
        {
            EditorGUI.HelpBox(Next(EditorGUIUtility.singleLineHeight * 2f),
                "No SceneDatabase found (t:SceneDatabase). Create one to use location dropdown.",
                MessageType.Warning);
            return y;
        }

        if (ids.Length == 0)
        {
            EditorGUI.HelpBox(Next(EditorGUIUtility.singleLineHeight * 2f),
                "SceneDatabase has no Locations with an Id. Fill LocationEntry.Id fields.",
                MessageType.Info);
            return y;
        }

        // Current selection
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allowedLocIdsProp.arraySize; i++)
            selected.Add(allowedLocIdsProp.GetArrayElementAtIndex(i).stringValue);

        // Buttons row
        var btn = Next(EditorGUIUtility.singleLineHeight);
        var half = (btn.width - 6f) * 0.5f;
        var clearR = new Rect(btn.x, btn.y, half, btn.height);
        var allR = new Rect(btn.x + half + 6f, btn.y, half, btn.height);

        if (GUI.Button(clearR, "Clear (Everywhere)"))
            selected.Clear();

        if (GUI.Button(allR, "Select All"))
        {
            selected.Clear();
            foreach (var id in ids) selected.Add(id);
        }

        // Checkbox list (cap)
        int shown = Mathf.Min(ids.Length, 20);
        for (int i = 0; i < shown; i++)
        {
            var id = ids[i];
            bool has = selected.Contains(id);
            bool next = EditorGUI.ToggleLeft(Next(EditorGUIUtility.singleLineHeight), id, has);
            if (next != has)
            {
                if (next) selected.Add(id);
                else selected.Remove(id);
            }
        }

        if (ids.Length > shown)
            EditorGUI.LabelField(Next(EditorGUIUtility.singleLineHeight), $"… {ids.Length - shown} more");

        // Write back (sorted for stable diffs)
        var finalList = selected.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        allowedLocIdsProp.arraySize = finalList.Count;
        for (int i = 0; i < finalList.Count; i++)
            allowedLocIdsProp.GetArrayElementAtIndex(i).stringValue = finalList[i];

        return y;
    }

    private static bool GetFoldout(SerializedProperty property)
    {
        if (_locFoldouts.TryGetValue(property.propertyPath, out var v)) return v;
        return false;
    }

    private static void SetFoldout(SerializedProperty property, bool value)
    {
        _locFoldouts[property.propertyPath] = value;
    }

    private static string[] GetLocationIds(SceneDatabase db)
    {
        if (db == null || db.Locations == null) return Array.Empty<string>();

        return db.Locations
            .Where(l => !string.IsNullOrEmpty(l.Id))
            .Select(l => l.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

}
#endif
