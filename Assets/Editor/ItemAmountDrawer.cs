#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ItemAmount))]
public class ItemAmountDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var itemIdProp = property.FindPropertyRelative("ItemId");
        var countProp = property.FindPropertyRelative("Count");

        var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
        var fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
            position.width - EditorGUIUtility.labelWidth, position.height);

        EditorGUI.PrefixLabel(labelRect, label);

        float spacing = 6f;
        float countWidth = 70f;
        float itemWidth = fieldRect.width - countWidth - spacing;

        var itemRect = new Rect(fieldRect.x, fieldRect.y, itemWidth, fieldRect.height);
        var countRect = new Rect(fieldRect.x + itemWidth + spacing, fieldRect.y, countWidth, fieldRect.height);

        EditorGUI.PropertyField(itemRect, itemIdProp, GUIContent.none);
        countProp.intValue = Mathf.Max(1, EditorGUI.IntField(countRect, countProp.intValue));

        EditorGUI.EndProperty();
    }
}
#endif