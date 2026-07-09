#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a DialogueSelectorRule as a one-line summary (tier strip + what plays +
/// where/when + which gates apply) that expands into grouped fields. With 30+
/// rules per NPC, the collapsed summaries are what keep the list scannable.
/// Placement (where the NPC stands) and dialogue live on the same rule, so the
/// "Where &amp; When" section is drawn as one unit with day/phase toggle strips.
/// </summary>
[CustomPropertyDrawer(typeof(DialogueSelectorRule))]
public class DialogueSelectorRuleDrawer : PropertyDrawer
{
    private const float Pad = 2f;
    private const float SectionGap = 6f;
    private const float ToggleRowH = 22f;
    private static float Line => EditorGUIUtility.singleLineHeight;

    private static readonly string[] DayLabels   = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly int[]    DayBits     = { 1, 2, 4, 8, 16, 32, 64 };   // DayOfWeekMask values
    private static readonly string[] PhaseLabels = { "Morning", "Afternoon", "Evening", "Night" };
    private static readonly int[]    PhaseBits   = { 1, 2, 4, 8 };               // DayPhaseMask values

    private static readonly Color ColSelected   = new Color(0.25f, 0.60f, 1.00f, 1f);
    private static readonly Color ColDeselected = new Color(0.30f, 0.30f, 0.30f, 1f);

    // Field names drawn in expanded mode, in order, with contextual skips.
    private static readonly string[] GateFields =
    {
        "requireNotSeenDialogue", "requireNotSeenThis", "requireSeenThis",
        "requiredQuestId", "requiredQuestState", "requiredQuestStepId", "requiredQuestStepIndex",
        "requiredRelationshipLevel"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = Line + Pad; // header row
        if (!property.isExpanded)
            return h;

        h += FieldH(property, "output") + FieldH(property, "tier") + FieldH(property, "priority");
        h += SectionGap;

        if (Placement(property) != NpcPlacement.Hidden)
        {
            if (IsPool(property))
                h += FieldH(property, "pool") + FieldH(property, "pickMode");
            else
                h += FieldH(property, "graph");

            h += SectionGap;
        }

        // Where & When
        h += FieldH(property, "placement");
        if (Placement(property) == NpcPlacement.AtLocation)
            h += FieldH(property, "locationScene") + FieldH(property, "spawnPointKey");
        h += (ToggleRowH + Pad) * 2; // days + phases toggle strips

        h += SectionGap;

        for (int i = 0; i < GateFields.Length; i++)
            h += FieldH(property, GateFields[i]);

        return h + Pad;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;

        // ── Header: tier strip + foldout with summary ──
        EditorGUI.DrawRect(new Rect(position.x, y + 1, 3, Line - 2), TierColor(EffectiveTier(property)));

        var foldRect = new Rect(position.x + 8, y, position.width - 8, Line);
        property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, BuildSummary(property), true, EditorStyles.foldout);
        y += Line + Pad;

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        var content = new Rect(position.x + 12, y, position.width - 12, 0);

        DrawField(ref content, property, "output");
        DrawField(ref content, property, "tier");
        DrawField(ref content, property, "priority");
        content.y += SectionGap;

        if (Placement(property) != NpcPlacement.Hidden)
        {
            if (IsPool(property))
            {
                DrawField(ref content, property, "pool");
                DrawField(ref content, property, "pickMode");
            }
            else
            {
                DrawField(ref content, property, "graph");
            }

            content.y += SectionGap;
        }

        // ── Where & When ──
        DrawField(ref content, property, "placement");

        if (Placement(property) == NpcPlacement.AtLocation)
        {
            DrawField(ref content, property, "locationScene");
            DrawField(ref content, property, "spawnPointKey");
        }

        DrawToggleButtons(new Rect(content.x, content.y, content.width, ToggleRowH),
            property.FindPropertyRelative("allowedDays"), "Days", DayLabels, DayBits);
        content.y += ToggleRowH + Pad;

        DrawToggleButtons(new Rect(content.x, content.y, content.width, ToggleRowH),
            property.FindPropertyRelative("allowedPhases"), "Phases", PhaseLabels, PhaseBits);
        content.y += ToggleRowH + Pad;

        content.y += SectionGap;

        for (int i = 0; i < GateFields.Length; i++)
            DrawField(ref content, property, GateFields[i]);

        EditorGUI.EndProperty();
    }

    // -----------------------
    // Field helpers
    // -----------------------

    private static float FieldH(SerializedProperty property, string field)
    {
        var p = property.FindPropertyRelative(field);
        return p != null ? EditorGUI.GetPropertyHeight(p, true) + Pad : 0f;
    }

    private static void DrawField(ref Rect cursor, SerializedProperty property, string field)
    {
        var p = property.FindPropertyRelative(field);
        if (p == null) return;

        float h = EditorGUI.GetPropertyHeight(p, true);
        EditorGUI.PropertyField(new Rect(cursor.x, cursor.y, cursor.width, h), p, true);
        cursor.y += h + Pad;
    }

    private static void DrawToggleButtons(
        Rect rowRect, SerializedProperty maskProp, string rowLabel,
        string[] labels, int[] bits)
    {
        if (maskProp == null) return;

        const float BtnH = 20f;
        float labelW = 50f;
        EditorGUI.LabelField(new Rect(rowRect.x, rowRect.y, labelW, rowRect.height), rowLabel);

        float btnW = (rowRect.width - labelW) / labels.Length;
        float bx = rowRect.x + labelW;
        float by = rowRect.y + (rowRect.height - BtnH) * 0.5f;

        int current = maskProp.intValue;

        for (int i = 0; i < labels.Length; i++)
        {
            bool isOn = (current & bits[i]) != 0;
            var btnRect = new Rect(bx + i * btnW + 1, by, btnW - 2, BtnH);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = isOn ? ColSelected : ColDeselected;

            if (GUI.Button(btnRect, labels[i]))
                maskProp.intValue = isOn ? current & ~bits[i] : current | bits[i];

            GUI.backgroundColor = prev;
        }
    }

    private static bool IsPool(SerializedProperty property)
    {
        var output = property.FindPropertyRelative("output");
        return output != null && output.intValue == (int)DialogueRuleOutput.Pool;
    }

    private static NpcPlacement Placement(SerializedProperty property)
    {
        var p = property.FindPropertyRelative("placement");
        return p != null ? (NpcPlacement)p.intValue : NpcPlacement.WhereverNpcIs;
    }

    // -----------------------
    // Summary
    // -----------------------

    private static DialogueRuleTier EffectiveTier(SerializedProperty property)
    {
        var tierProp = property.FindPropertyRelative("tier");
        var tier = tierProp != null ? (DialogueRuleTier)tierProp.intValue : DialogueRuleTier.Auto;

        if (tier != DialogueRuleTier.Auto)
            return tier;

        // Mirror DialogueSelectorRule.GetEffectiveTier for Auto.
        string questId = property.FindPropertyRelative("requiredQuestId")?.stringValue;
        var state = (QuestRouteState)(property.FindPropertyRelative("requiredQuestState")?.intValue ?? 0);
        string stepId = property.FindPropertyRelative("requiredQuestStepId")?.stringValue;
        int stepIndex = property.FindPropertyRelative("requiredQuestStepIndex")?.intValue ?? -1;

        bool activeQuestGated = !string.IsNullOrWhiteSpace(questId) &&
            (state == QuestRouteState.Active || !string.IsNullOrWhiteSpace(stepId) || stepIndex >= 0);

        return activeQuestGated ? DialogueRuleTier.Quest : DialogueRuleTier.Routine;
    }

    private static Color TierColor(DialogueRuleTier tier) => tier switch
    {
        DialogueRuleTier.Quest => new Color(0.85f, 0.45f, 0.20f),
        DialogueRuleTier.Event => new Color(0.25f, 0.65f, 0.65f),
        DialogueRuleTier.Routine => new Color(0.35f, 0.50f, 0.75f),
        DialogueRuleTier.SmallTalk => new Color(0.55f, 0.55f, 0.55f),
        _ => Color.gray
    };

    private static string BuildSummary(SerializedProperty property)
    {
        var sb = new StringBuilder(96);

        sb.Append('[').Append(EffectiveTier(property)).Append("] ");

        var placement = Placement(property);

        // What plays
        if (placement == NpcPlacement.Hidden)
        {
            sb.Append("Hidden (NPC absent)");
        }
        else if (IsPool(property))
        {
            var pool = property.FindPropertyRelative("pool");
            int count = pool != null ? pool.arraySize : 0;
            var pick = (DialoguePickMode)(property.FindPropertyRelative("pickMode")?.intValue ?? 0);
            sb.Append(count > 0 ? $"Pool({count}) {pick}" : "⚠ empty pool");
        }
        else
        {
            var graph = property.FindPropertyRelative("graph")?.objectReferenceValue;
            if (graph != null)
                sb.Append(graph.name);
            else
                sb.Append(placement == NpcPlacement.AtLocation ? "presence only" : "⚠ no graph");
        }

        // Where
        if (placement == NpcPlacement.AtLocation)
        {
            var sceneAsset = property.FindPropertyRelative("locationScene")?
                .FindPropertyRelative("sceneAsset")?.objectReferenceValue;
            string spawn = property.FindPropertyRelative("spawnPointKey")?.stringValue;

            sb.Append("  @ ").Append(sceneAsset != null ? sceneAsset.name : "⚠ no location");
            if (!string.IsNullOrWhiteSpace(spawn))
                sb.Append(':').Append(spawn);
        }

        // Gates
        var gates = new List<string>(4);

        string questId = property.FindPropertyRelative("requiredQuestId")?.stringValue;
        if (!string.IsNullOrWhiteSpace(questId))
        {
            var state = (QuestRouteState)(property.FindPropertyRelative("requiredQuestState")?.intValue ?? 0);
            string stepId = property.FindPropertyRelative("requiredQuestStepId")?.stringValue;
            string quest = state == QuestRouteState.Any ? questId : $"{questId}={state}";
            if (!string.IsNullOrWhiteSpace(stepId)) quest += $"@{stepId}";
            gates.Add(quest);
        }

        int rel = property.FindPropertyRelative("requiredRelationshipLevel")?.intValue ?? 0;
        if (rel > 0) gates.Add($"rel≥{rel}");

        if (property.FindPropertyRelative("requireNotSeenDialogue")?.boolValue == true) gates.Add("unseen");
        if (property.FindPropertyRelative("requireNotSeenThis")?.objectReferenceValue != null) gates.Add("not-seen-other");
        if (property.FindPropertyRelative("requireSeenThis")?.objectReferenceValue != null) gates.Add("after-seen");

        var phases = property.FindPropertyRelative("allowedPhases");
        if (phases != null && phases.intValue != (int)DayPhaseMask.All) gates.Add("phases");

        var days = property.FindPropertyRelative("allowedDays");
        if (days != null && days.intValue != (int)DayOfWeekMask.All) gates.Add("days");

        if (gates.Count > 0)
            sb.Append("  ·  ").Append(string.Join(", ", gates));
        else
            sb.Append("  ·  always");

        return sb.ToString();
    }
}
#endif
