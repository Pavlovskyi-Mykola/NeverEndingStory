using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogueCommand))]
public class DialogueCommandDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight;
    private const float VSpace = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative("type");
        if (typeProp == null)
            return Line;

        string typeName = GetEnumName(typeProp);

        switch (typeName)
        {
            case "SetFlag":
                return (Line + VSpace) * 3f;

            case "AddItem":
            case "RemoveItem":
            case "ConsumeItem":
            case "AddRelationship":
                return (Line + VSpace) * 3f;

            case "StartQuest":
                return (Line + VSpace) * 2f;

            default:
                return (Line + VSpace) * 2f;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative("type");
        var amountProp = property.FindPropertyRelative("amount");
        var flagIdProp = property.FindPropertyRelative("flagId");
        var flagValueProp = property.FindPropertyRelative("flagValue");
        var itemIdProp = property.FindPropertyRelative("itemId");
        var questIdProp = property.FindPropertyRelative("questId");

        if (typeProp == null)
        {
            EditorGUI.LabelField(position, label.text, "DialogueCommand fields not found.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        Rect row = new Rect(position.x, position.y, position.width, Line);
        EditorGUI.PropertyField(row, typeProp, label);

        row.y += Line + VSpace;

        string typeName = GetEnumName(typeProp);

        switch (typeName)
        {
            case "AddMoney":
            case "SpendMoney":
            case "AddStrength":
            case "AddIntellect":
                if (amountProp != null)
                    EditorGUI.PropertyField(row, amountProp, new GUIContent("Amount"));
                break;

            case "AdvanceTimePhase":
                EditorGUI.LabelField(row, "No extra parameters");
                break;

            case "SetFlag":
                if (flagIdProp != null)
                    WorldFlagEditorUtility.DrawFlagField(row, flagIdProp, new GUIContent("Flag Id"));

                row.y += Line + VSpace;

                if (flagValueProp != null)
                    EditorGUI.PropertyField(row, flagValueProp, new GUIContent("Value"));
                break;

            case "AddItem":
            case "RemoveItem":
            case "ConsumeItem":
                if (itemIdProp != null)
                    EditorGUI.PropertyField(row, itemIdProp, new GUIContent("Item"));

                row.y += Line + VSpace;

                if (amountProp != null)
                    EditorGUI.PropertyField(row, amountProp, new GUIContent("Count"));
                break;

            case "StartQuest":
                if (questIdProp != null)
                    DrawQuestPopup(row, questIdProp);
                else
                    EditorGUI.LabelField(row, "questId field is missing");
                break;

            case "AddRelationship":
                if (amountProp != null)
                    EditorGUI.PropertyField(row, amountProp, new GUIContent("Points", "Negative = penalty"));

                row.y += Line + VSpace;

                var targetNpcProp = property.FindPropertyRelative("targetNpcId");
                if (targetNpcProp != null)
                    EditorGUI.PropertyField(row, targetNpcProp, new GUIContent("Npc Id", "Empty = current speaker"));
                break;

            default:
                EditorGUI.LabelField(row, "Unsupported command type");
                break;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawQuestPopup(Rect rect, SerializedProperty questIdProp)
    {
        BuildQuestOptions(out var labels, out var values);

        if (values.Count == 0)
        {
            EditorGUI.HelpBox(rect, "No QuestDefinition assets found.", MessageType.Warning);
            return;
        }

        string current = questIdProp.stringValue ?? "";
        int currentIndex = 0;

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], current, System.StringComparison.Ordinal))
            {
                currentIndex = i;
                break;
            }
        }

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUI.Popup(rect, "Quest", currentIndex, labels.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            questIdProp.stringValue = values[Mathf.Clamp(nextIndex, 0, values.Count - 1)];
        }
    }

    private static void BuildQuestOptions(out List<string> labels, out List<string> values)
    {
        labels = new List<string>();
        values = new List<string>();

        labels.Add("<None>");
        values.Add("");

        string[] guids = AssetDatabase.FindAssets("t:QuestDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (quest == null)
                continue;

            if (string.IsNullOrWhiteSpace(quest.QuestId))
                continue;

            if (values.Contains(quest.QuestId))
                continue;

            labels.Add($"{quest.QuestId} ({quest.name})");
            values.Add(quest.QuestId);
        }
    }

    private static string GetEnumName(SerializedProperty enumProp)
    {
        if (enumProp.propertyType != SerializedPropertyType.Enum)
            return string.Empty;

        int index = enumProp.enumValueIndex;
        if (index < 0 || index >= enumProp.enumNames.Length)
            return string.Empty;

        return enumProp.enumNames[index];
    }
}