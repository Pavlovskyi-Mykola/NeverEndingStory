using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FlagChange))]
public class FlagChangeDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight;
    private const float VSpace = 2f;
    private const float ToggleWidth = 60f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (Line + VSpace) * 2f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var flagIdProp = property.FindPropertyRelative("FlagId");
        var valueProp = property.FindPropertyRelative("Value");

        if (flagIdProp == null || valueProp == null)
        {
            EditorGUI.LabelField(position, label.text, "FlagChange fields not found.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        Rect row1 = new Rect(position.x, position.y, position.width, Line);
        Rect row2 = new Rect(position.x, position.y + Line + VSpace, position.width, Line);

        EditorGUI.LabelField(row1, label);

        float leftWidth = row2.width - ToggleWidth - 4f;
        Rect flagRect = new Rect(row2.x, row2.y, leftWidth, row2.height);
        Rect toggleRect = new Rect(flagRect.xMax + 4f, row2.y, ToggleWidth, row2.height);

        WorldFlagEditorUtility.DrawFlagField(flagRect, flagIdProp, new GUIContent("Flag Id"));
        valueProp.boolValue = EditorGUI.ToggleLeft(toggleRect, "True", valueProp.boolValue);

        EditorGUI.EndProperty();
    }
}