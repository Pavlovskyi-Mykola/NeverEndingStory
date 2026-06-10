#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LocationEntry))]
public class LocationEntryDrawer : PropertyDrawer
{
    private static readonly string[] DayLabels   = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly int[]    DayBits     = { 1, 2, 4, 8, 16, 32, 64 };

    private static readonly string[] PhaseLabels = { "Morning", "Afternoon", "Evening", "Night" };
    private static readonly int[]    PhaseBits   = { 1, 2, 4, 8 };

    private const float RowH = 22f;
    private const float Pad  = 3f;
    private const float BtnH = 20f;

    private static readonly Color ColSelected   = new Color(0.25f, 0.60f, 1.00f, 1f);
    private static readonly Color ColDeselected = new Color(0.30f, 0.30f, 0.30f, 1f);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var idProp    = property.FindPropertyRelative("Id");
        var sceneProp = property.FindPropertyRelative("Scene");
        var itemsProp = property.FindPropertyRelative("RequiredItems");

        return EditorGUI.GetPropertyHeight(idProp,    true) + Pad
             + EditorGUI.GetPropertyHeight(sceneProp, true) + Pad
             + RowH + Pad    // AllowedDays buttons
             + RowH + Pad    // AllowedPhases buttons
             + EditorGUI.GetPropertyHeight(itemsProp, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var idProp     = property.FindPropertyRelative("Id");
        var sceneProp  = property.FindPropertyRelative("Scene");
        var daysProp   = property.FindPropertyRelative("AllowedDays");
        var phasesProp = property.FindPropertyRelative("AllowedPhases");
        var itemsProp  = property.FindPropertyRelative("RequiredItems");

        float x = position.x;
        float y = position.y;
        float w = position.width;

        float idH = EditorGUI.GetPropertyHeight(idProp, true);
        EditorGUI.PropertyField(new Rect(x, y, w, idH), idProp, true);
        y += idH + Pad;

        float sceneH = EditorGUI.GetPropertyHeight(sceneProp, true);
        EditorGUI.PropertyField(new Rect(x, y, w, sceneH), sceneProp, true);
        y += sceneH + Pad;

        y = DrawToggleButtons(new Rect(x, y, w, RowH), daysProp,   "Days",   DayLabels,   DayBits);
        y += Pad;

        y = DrawToggleButtons(new Rect(x, y, w, RowH), phasesProp, "Phases", PhaseLabels, PhaseBits);
        y += Pad;

        float itemsH = EditorGUI.GetPropertyHeight(itemsProp, true);
        EditorGUI.PropertyField(new Rect(x, y, w, itemsH), itemsProp, true);

        EditorGUI.EndProperty();
    }

    private static float DrawToggleButtons(
        Rect rowRect, SerializedProperty maskProp, string rowLabel,
        string[] labels, int[] bits)
    {
        int   count    = labels.Length;
        float labelW   = 50f;
        EditorGUI.LabelField(new Rect(rowRect.x, rowRect.y, labelW, rowRect.height), rowLabel);

        float available = rowRect.width - labelW;
        float btnW      = available / count;
        float bx        = rowRect.x + labelW;
        float by        = rowRect.y + (rowRect.height - BtnH) * 0.5f;

        int current = maskProp.intValue;

        for (int i = 0; i < count; i++)
        {
            bool  isOn    = (current & bits[i]) != 0;
            var   btnRect = new Rect(bx + i * btnW + 1, by, btnW - 2, BtnH);
            Color prev    = GUI.backgroundColor;

            GUI.backgroundColor = isOn ? ColSelected : ColDeselected;

            if (GUI.Button(btnRect, labels[i]))
                maskProp.intValue = isOn ? current & ~bits[i] : current | bits[i];

            GUI.backgroundColor = prev;
        }

        return rowRect.y + rowRect.height;
    }
}
#endif
