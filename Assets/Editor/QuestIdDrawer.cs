using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(QuestIdAttribute))]
public class QuestIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // For list elements the label is "Element N"; keep it so rows stay aligned.
        QuestIdEditorUtility.DrawQuestIdField(position, property, label);
    }
}
