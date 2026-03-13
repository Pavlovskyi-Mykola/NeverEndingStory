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
                return (Line + VSpace) * 3f;

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

            default:
                EditorGUI.LabelField(row, "Unsupported command type");
                break;
        }

        EditorGUI.EndProperty();
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