using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FlagIdAttribute))]
public class FlagIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        WorldFlagEditorUtility.DrawFlagField(position, property, label);
    }
}
