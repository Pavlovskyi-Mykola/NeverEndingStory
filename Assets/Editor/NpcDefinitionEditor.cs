#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for NpcDefinition focused on making large dialogue-routing
/// lists (30+ rules) workable: validation summary, search filter over rule
/// summaries, and a "sort to runtime order" utility. Rule rows themselves render
/// via DialogueSelectorRuleDrawer (one-line summaries, expandable).
/// </summary>
[CustomEditor(typeof(NpcDefinition))]
public class NpcDefinitionEditor : Editor
{
    private SerializedProperty _rulesProp;
    private SerializedProperty _fallbackProp;
    private string _search = "";

    private void OnEnable()
    {
        _rulesProp = serializedObject.FindProperty("DialogueRules");
        _fallbackProp = serializedObject.FindProperty("DialogueFallback");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Everything except routing draws normally (identity, prefab, schedule, relationship).
        DrawPropertiesExcluding(serializedObject, "m_Script", "DialogueRules", "DialogueFallback");

        EditorGUILayout.Space(12);
        DrawRoutingSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRoutingSection()
    {
        var npc = (NpcDefinition)target;
        int count = npc.DialogueRules?.Count ?? 0;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Dialogue Routing ({count} rules)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(count < 2))
            {
                if (GUILayout.Button(new GUIContent("Sort to runtime order",
                        "Reorders the list by tier, then priority — the order the selector evaluates in."),
                    EditorStyles.miniButton, GUILayout.Width(140)))
                    SortRulesToRuntimeOrder(npc);
            }
        }

        DrawValidation(npc);

        EditorGUILayout.PropertyField(_fallbackProp,
            new GUIContent("Dialogue Fallback", "Played when no rule resolves a graph."));

        EditorGUILayout.Space(4);

        _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);

        if (string.IsNullOrWhiteSpace(_search))
        {
            // Normal reorderable list; rows render via DialogueSelectorRuleDrawer.
            EditorGUILayout.PropertyField(_rulesProp, new GUIContent("Rules"), true);
            return;
        }

        // Filtered view: matching rules only (editable in place; reorder via the
        // unfiltered list). Matches graph/pool names, quest ids, and tier names.
        string search = _search.ToLowerInvariant();
        int shown = 0;

        for (int i = 0; i < _rulesProp.arraySize; i++)
        {
            if (!RuleMatches(npc, i, search))
                continue;

            shown++;
            EditorGUILayout.PropertyField(_rulesProp.GetArrayElementAtIndex(i),
                new GUIContent($"#{i}"), true);
        }

        if (shown == 0)
            EditorGUILayout.HelpBox("No rules match the search.", MessageType.Info);
        else
            EditorGUILayout.LabelField($"{shown} of {_rulesProp.arraySize} rules shown (clear search to reorder/add).",
                EditorStyles.miniLabel);
    }

    private static bool RuleMatches(NpcDefinition npc, int index, string search)
    {
        if (npc.DialogueRules == null || index >= npc.DialogueRules.Count)
            return false;

        var rule = npc.DialogueRules[index];
        if (rule == null)
            return false;

        if (rule.graph != null && rule.graph.name.ToLowerInvariant().Contains(search))
            return true;

        if (rule.pool != null)
        {
            for (int i = 0; i < rule.pool.Length; i++)
            {
                if (rule.pool[i] != null && rule.pool[i].name.ToLowerInvariant().Contains(search))
                    return true;
            }
        }

        if (!string.IsNullOrEmpty(rule.requiredQuestId) &&
            rule.requiredQuestId.ToLowerInvariant().Contains(search))
            return true;

        return rule.GetEffectiveTier().ToString().ToLowerInvariant().Contains(search);
    }

    private void DrawValidation(NpcDefinition npc)
    {
        if (npc.DialogueRules == null || npc.DialogueRules.Count == 0)
        {
            if (npc.DialogueFallback == null)
                EditorGUILayout.HelpBox("No rules and no fallback — this NPC has nothing to say.", MessageType.Warning);
            return;
        }

        int broken = 0;
        for (int i = 0; i < npc.DialogueRules.Count; i++)
        {
            var rule = npc.DialogueRules[i];
            if (rule == null) { broken++; continue; }

            bool hasOutput = rule.output == DialogueRuleOutput.Pool
                ? rule.pool != null && rule.pool.Any(g => g != null)
                : rule.graph != null;

            if (!hasOutput) broken++;
        }

        if (broken > 0)
            EditorGUILayout.HelpBox($"{broken} rule(s) have no graph/pool assigned — they can never play.", MessageType.Warning);

        if (npc.DialogueFallback == null)
            EditorGUILayout.HelpBox("No Dialogue Fallback — if no rule matches, the NPC won't talk.", MessageType.Info);
    }

    private void SortRulesToRuntimeOrder(NpcDefinition npc)
    {
        Undo.RecordObject(npc, "Sort Dialogue Rules");

        // Stable sort: tier ascending, then priority descending — matches
        // DialogueSelector's evaluation order (ties keep authored order).
        var sorted = npc.DialogueRules
            .Select((rule, index) => (rule, index))
            .OrderBy(x => x.rule != null ? (int)x.rule.GetEffectiveTier() : int.MaxValue)
            .ThenByDescending(x => x.rule != null ? x.rule.priority : int.MinValue)
            .ThenBy(x => x.index)
            .Select(x => x.rule)
            .ToList();

        npc.DialogueRules.Clear();
        npc.DialogueRules.AddRange(sorted);

        EditorUtility.SetDirty(npc);
        serializedObject.Update();
    }
}
#endif
