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

            default:
                return (Line + VSpace) * 2f;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative("type");
        var intValueProp = property.FindPropertyRelative("intValue");
        var stringValueProp = property.FindPropertyRelative("stringValue");
        var boolValueProp = property.FindPropertyRelative("boolValue");

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
                if (intValueProp != null)
                    EditorGUI.PropertyField(row, intValueProp, new GUIContent("Amount"));
                break;

            case "AdvanceTimePhase":
                EditorGUI.LabelField(row, "No extra parameters");
                break;

            case "SetFlag":
                if (stringValueProp != null)
                    WorldFlagEditorUtility.DrawFlagField(row, stringValueProp, new GUIContent("Flag Id"));

                row.y += Line + VSpace;

                if (boolValueProp != null)
                    EditorGUI.PropertyField(row, boolValueProp, new GUIContent("Value"));
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