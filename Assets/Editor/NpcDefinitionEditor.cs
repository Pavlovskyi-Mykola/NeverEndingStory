#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for NpcDefinition. The heavy lifting (rule rows, validation,
/// sorting, search) is shared with NpcRoutingWindow — the dedicated editor with
/// the NPC list on the left — via DialogueSelectorRuleDrawer,
/// NpcRoutingValidation and NpcRoutingEditorUtility. This inspector stays a
/// lightweight view for quick edits from the Project window.
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

        if (GUILayout.Button("Open in NPC Routing window"))
            NpcRoutingWindow.OpenNpc((NpcDefinition)target);

        EditorGUILayout.Space(4);

        // Everything except routing draws normally (identity, prefab, relationship).
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
                {
                    NpcRoutingEditorUtility.SortRulesToRuntimeOrder(npc);
                    serializedObject.Update();
                }
            }
        }

        foreach (var msg in NpcRoutingValidation.Validate(npc))
            EditorGUILayout.HelpBox(msg.Text, msg.Type);

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
        // unfiltered list). Matches graph/pool names, quest ids, locations, tiers.
        string search = _search.ToLowerInvariant();
        int shown = 0;

        for (int i = 0; i < _rulesProp.arraySize; i++)
        {
            if (!NpcRoutingEditorUtility.RuleMatches(npc, i, search))
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
}
#endif
