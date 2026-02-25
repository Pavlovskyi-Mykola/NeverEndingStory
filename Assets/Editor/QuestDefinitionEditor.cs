#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(QuestDefinition))]
public class QuestDefinitionEditor : Editor
{
    private SerializedProperty _questId;
    private SerializedProperty _title;
    private SerializedProperty _desc;
    private SerializedProperty _steps;

    private ReorderableList _list;

    private void OnEnable()
    {
        _questId = serializedObject.FindProperty("QuestId");
        _title = serializedObject.FindProperty("Title");
        _desc = serializedObject.FindProperty("Description");
        _steps = serializedObject.FindProperty("Steps");

        _list = new ReorderableList(serializedObject, _steps, true, true, true, true);
        _list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Quest Steps");

        _list.elementHeightCallback = index =>
        {
            var el = _steps.GetArrayElementAtIndex(index);

            if (!el.isExpanded)
                return EditorGUIUtility.singleLineHeight + 6;

            // Dynamic height: sum only visible fields
            return CalcExpandedHeight(el) + 16;
        };

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = _steps.GetArrayElementAtIndex(index);
            var typeProp = el.FindPropertyRelative("Type");
            var textProp = el.FindPropertyRelative("Text");

            rect.y += 2;

            // Foldout header
            var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            string label = $"{index + 1}. {GetEnumDisplay(typeProp)}";
            string preview = (textProp?.stringValue ?? "").Replace("\n", " ").Replace("\r", " ");
            if (preview.Length > 42) preview = preview.Substring(0, 39) + "...";
            if (!string.IsNullOrEmpty(preview)) label += $" — {preview}";
            el.isExpanded = EditorGUI.Foldout(headerRect, el.isExpanded, label, true);

            if (!el.isExpanded) return;

            float y = headerRect.yMax + 4;

            // Draw only relevant fields
            y = DrawExpanded(el, new Rect(rect.x, y, rect.width, rect.height - (y - rect.y)));
        };

        _list.onAddDropdownCallback = (buttonRect, l) =>
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Manual"), false, () => AddStep(QuestStepType.Manual));
            menu.AddItem(new GUIContent("Reach Location"), false, () => AddStep(QuestStepType.ReachLocation));
            menu.AddItem(new GUIContent("Min Stats"), false, () => AddStep(QuestStepType.MinStats));
            menu.AddItem(new GUIContent("Require Money"), false, () => AddStep(QuestStepType.HaveMoney));
            menu.AddItem(new GUIContent("Pay Money"), false, () => AddStep(QuestStepType.PayMoney));
            menu.AddItem(new GUIContent("AutoComplete"), false, () => AddStep(QuestStepType.AutoComplete));
            menu.DropDown(buttonRect);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_questId);
        EditorGUILayout.PropertyField(_title);
        EditorGUILayout.PropertyField(_desc);

        EditorGUILayout.Space(8);

        if (string.IsNullOrEmpty(_questId.stringValue))
            EditorGUILayout.HelpBox("QuestId is empty. Set a unique id (e.g., q_bob_intro).", MessageType.Error);

        _list.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    // ----------------------
    // Drawing helpers
    // ----------------------

    private static float CalcExpandedHeight(SerializedProperty stepEl)
    {
        float h = 0;

        var stepId = stepEl.FindPropertyRelative("StepId");
        h += PropHeight(stepId);
        h += Spacing();

        h += PropHeight(stepEl.FindPropertyRelative("Type"));
        h += Spacing();

        h += PropHeight(stepEl.FindPropertyRelative("Text"), includeChildren: true);
        h += Spacing();

        var type = GetStepType(stepEl);

        // Type-specific
        switch (type)
        {
            case QuestStepType.ReachLocation:
                h += PropHeight(stepEl.FindPropertyRelative("TargetLocation"), includeChildren: true);
                h += Spacing();
                break;

            case QuestStepType.MinStats:
                h += PropHeight(stepEl.FindPropertyRelative("RequiredStrength"));
                h += Spacing();
                h += PropHeight(stepEl.FindPropertyRelative("RequiredIntellect"));
                h += Spacing();
                break;

            case QuestStepType.HaveMoney:
            case QuestStepType.PayMoney:
                h += PropHeight(stepEl.FindPropertyRelative("RequiredMoney"));
                h += Spacing();
                break;
        }

        // Time restriction block (always show toggles, show masks only if enabled)
        h += PropHeight(stepEl.FindPropertyRelative("RestrictByDay"));
        h += Spacing();
        if (stepEl.FindPropertyRelative("RestrictByDay").boolValue)
        {
            h += PropHeight(stepEl.FindPropertyRelative("AllowedDays"));
            h += Spacing();
        }

        h += PropHeight(stepEl.FindPropertyRelative("RestrictByPhase"));
        h += Spacing();
        if (stepEl.FindPropertyRelative("RestrictByPhase").boolValue)
        {
            h += PropHeight(stepEl.FindPropertyRelative("AllowedPhases"));
            h += Spacing();
        }

        // Validation helpbox (only if needed)
        var msg = Validate(stepEl);
        if (!string.IsNullOrEmpty(msg))
        {
            h += HelpBoxHeight();
            h += Spacing();
        }

        return h;
    }

    private static float DrawExpanded(SerializedProperty stepEl, Rect rect)
    {
        float innerPadding = 6f;
        rect.x += innerPadding;
        rect.width -= innerPadding * 2;
        float y = rect.y;

        // StepId + Auto ID button row
        var stepId = stepEl.FindPropertyRelative("StepId");

        float idH = PropHeight(stepId); // dynamic height

        var idRect = new Rect(rect.x, y, rect.width - 70, idH);
        var btnRect = new Rect(rect.x + rect.width - 65, y, 65, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(idRect, stepId);

        // keep the button aligned to top of the row
        if (GUI.Button(btnRect, "Auto ID"))
            stepId.stringValue = $"step_{stepEl.propertyPath.GetHashCode():X8}";

        y += idH + Spacing();

        // Type
        y = DrawProp(stepEl.FindPropertyRelative("Type"), rect.x, y, rect.width);

        // Text
        y = DrawProp(stepEl.FindPropertyRelative("Text"), rect.x, y, rect.width, includeChildren: true);

        var type = GetStepType(stepEl);

        // Type-specific fields
        switch (type)
        {
            case QuestStepType.ReachLocation:
                y = DrawProp(stepEl.FindPropertyRelative("TargetLocation"), rect.x, y, rect.width, includeChildren: true);
                break;

            case QuestStepType.MinStats:
                y = DrawProp(stepEl.FindPropertyRelative("RequiredStrength"), rect.x, y, rect.width);
                y = DrawProp(stepEl.FindPropertyRelative("RequiredIntellect"), rect.x, y, rect.width);
                break;

            case QuestStepType.HaveMoney:
            case QuestStepType.PayMoney:
                y = DrawProp(stepEl.FindPropertyRelative("RequiredMoney"), rect.x, y, rect.width);
                break;
        }

        // Time restrictions block
        y = DrawProp(stepEl.FindPropertyRelative("RestrictByDay"), rect.x, y, rect.width);
        if (stepEl.FindPropertyRelative("RestrictByDay").boolValue)
            y = DrawProp(stepEl.FindPropertyRelative("AllowedDays"), rect.x, y, rect.width);

        y = DrawProp(stepEl.FindPropertyRelative("RestrictByPhase"), rect.x, y, rect.width);
        if (stepEl.FindPropertyRelative("RestrictByPhase").boolValue)
            y = DrawProp(stepEl.FindPropertyRelative("AllowedPhases"), rect.x, y, rect.width);

        // Validation helpbox
        var msg = Validate(stepEl);
        if (!string.IsNullOrEmpty(msg))
        {
            var hb = new Rect(rect.x, y, rect.width, HelpBoxHeight());
            EditorGUI.HelpBox(hb, msg, MessageType.Warning);
            y += HelpBoxHeight() + Spacing();
        }

        return y;
    }

    private static float DrawProp(SerializedProperty prop, float x, float y, float width, bool includeChildren = false)
    {
        if (prop == null) return y;

        float h = PropHeight(prop, includeChildren);
        var r = new Rect(x, y, width, h);
        EditorGUI.PropertyField(r, prop, includeChildren);
        return y + h + Spacing();
    }

    private static float PropHeight(SerializedProperty prop, bool includeChildren = false)
    {
        if (prop == null) return 0;
        return EditorGUI.GetPropertyHeight(prop, includeChildren);
    }

    private static float LineHeight() => EditorGUIUtility.singleLineHeight;
    private static float Spacing() => 8f;
    private static float HelpBoxHeight() => 42f;

    private static QuestStepType GetStepType(SerializedProperty stepEl)
    {
        var typeProp = stepEl.FindPropertyRelative("Type");
        if (typeProp == null) return QuestStepType.Manual;

        // IMPORTANT: our enum uses int values like 11,13,15 etc.
        return (QuestStepType)typeProp.intValue;
    }

    private static string GetEnumDisplay(SerializedProperty typeProp)
    {
        if (typeProp == null) return "Unknown";
        // enumValueIndex is safe for display names even if values are sparse
        int idx = Mathf.Clamp(typeProp.enumValueIndex, 0, typeProp.enumDisplayNames.Length - 1);
        return typeProp.enumDisplayNames[idx];
    }

    private static string Validate(SerializedProperty stepEl)
    {
        var type = GetStepType(stepEl);

        if (type == QuestStepType.ReachLocation)
        {
            var loc = stepEl.FindPropertyRelative("TargetLocation");

            // Guard: only ObjectReference supports objectReferenceValue
            if (loc == null)
                return "ReachLocation: TargetLocation field not found (check QuestStepDefinition field name).";

            if (loc.propertyType != SerializedPropertyType.ObjectReference)
                return "ReachLocation: TargetLocation is not an ObjectReference (check field type - should be SceneReference).";

            if (loc.objectReferenceValue == null)
                return "ReachLocation: TargetLocation is not set.";
        }

        if (type == QuestStepType.MinStats)
        {
            int s = stepEl.FindPropertyRelative("RequiredStrength")?.intValue ?? 0;
            int i = stepEl.FindPropertyRelative("RequiredIntellect")?.intValue ?? 0;
            if (s <= 0 && i <= 0)
                return "MinStats: set RequiredStrength and/or RequiredIntellect.";
        }

        if (type == QuestStepType.HaveMoney || type == QuestStepType.PayMoney)
        {
            int m = stepEl.FindPropertyRelative("RequiredMoney")?.intValue ?? 0;
            if (m <= 0)
                return "Money step: RequiredMoney should be > 0.";
        }

        bool rDay = stepEl.FindPropertyRelative("RestrictByDay")?.boolValue ?? false;
        if (rDay)
        {
            int days = stepEl.FindPropertyRelative("AllowedDays")?.intValue ?? 0;
            if (days == 0) return "RestrictByDay is enabled but AllowedDays is empty.";
        }

        bool rPhase = stepEl.FindPropertyRelative("RestrictByPhase")?.boolValue ?? false;
        if (rPhase)
        {
            int phases = stepEl.FindPropertyRelative("AllowedPhases")?.intValue ?? 0;
            if (phases == 0) return "RestrictByPhase is enabled but AllowedPhases is empty.";
        }

        return null;
    }

    // ----------------------
    // Add step templates
    // ----------------------

    private void AddStep(QuestStepType type)
    {
        serializedObject.Update();

        int idx = _steps.arraySize;
        _steps.arraySize++;
        var el = _steps.GetArrayElementAtIndex(idx);

        // Clear/init
        el.FindPropertyRelative("StepId").stringValue = $"step_{idx + 1:00}";
        el.FindPropertyRelative("Text").stringValue = "";

        // IMPORTANT: set enum by VALUE (sparse enum safe)
        var typeProp = el.FindPropertyRelative("Type");
        typeProp.intValue = (int)type;

        // Defaults
        var rDay = el.FindPropertyRelative("RestrictByDay");
        if (rDay != null) rDay.boolValue = false;

        var rPhase = el.FindPropertyRelative("RestrictByPhase");
        if (rPhase != null) rPhase.boolValue = false;

        // Expand new step by default
        el.isExpanded = true;

        serializedObject.ApplyModifiedProperties();
    }
}
#endif