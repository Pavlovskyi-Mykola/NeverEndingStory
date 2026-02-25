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

            // Collapsed: single line
            if (!el.isExpanded)
                return EditorGUIUtility.singleLineHeight + 6;

            // Expanded: ask Unity how tall this element is (includes children)
            return EditorGUI.GetPropertyHeight(el, includeChildren: true) + 30;
        };

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = _steps.GetArrayElementAtIndex(index);

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            // Nice compact label when collapsed
            var typeProp = el.FindPropertyRelative("Type");
            var textProp = el.FindPropertyRelative("Text");

            string label = $"{index + 1}. {typeProp.enumDisplayNames[typeProp.enumValueIndex]}";
            string t = textProp != null ? textProp.stringValue : "";
            t = (t ?? "").Replace("\n", " ").Replace("\r", " ");
            if (t.Length > 40) t = t.Substring(0, 37) + "...";
            if (!string.IsNullOrEmpty(t)) label += $" — {t}";

            el.isExpanded = EditorGUI.Foldout(rect, el.isExpanded, label, true);

            if (!el.isExpanded) return;

            // Draw full element (all fields) below foldout
            rect.y += EditorGUIUtility.singleLineHeight + 4;

            float fullHeight = EditorGUI.GetPropertyHeight(el, includeChildren: true);
            var fullRect = new Rect(rect.x, rect.y, rect.width, fullHeight);

            EditorGUI.PropertyField(fullRect, el, GUIContent.none, includeChildren: true);
        };

        // Add dropdown templates
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

        EditorGUILayout.Space(5);

        if (string.IsNullOrEmpty(_questId.stringValue))
            EditorGUILayout.HelpBox("QuestId is empty. Set a unique id (e.g., q_bob_intro).", MessageType.Error);

        _list.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private void AddStep(QuestStepType type)
    {
        serializedObject.Update();

        int idx = _steps.arraySize;
        _steps.arraySize++;
        var el = _steps.GetArrayElementAtIndex(idx);

        // IMPORTANT:
        // Insert/Resize can clone previous element. Clear to defaults:
        el.FindPropertyRelative("StepId").stringValue = $"step_{idx + 1:00}";
        el.FindPropertyRelative("Text").stringValue = "";

        var typeProp = el.FindPropertyRelative("Type");
        typeProp.intValue = (int)type;

        // Reasonable defaults
        var rDay = el.FindPropertyRelative("RestrictByDay");
        if (rDay != null) rDay.boolValue = false;

        var rPhase = el.FindPropertyRelative("RestrictByPhase");
        if (rPhase != null) rPhase.boolValue = false;

        // Optional: expand newly added element
        el.isExpanded = true;

        serializedObject.ApplyModifiedProperties();
    }
}
#endif