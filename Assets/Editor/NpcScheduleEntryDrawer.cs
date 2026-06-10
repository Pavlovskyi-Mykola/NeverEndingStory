#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NpcScheduleEntry))]
public class NpcScheduleEntryDrawer : PropertyDrawer
{
    private static readonly string[] DayLabels   = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly int[]    DayBits     = { 1, 2, 4, 8, 16, 32, 64 };   // DayOfWeekMask values

    private static readonly string[] PhaseLabels = { "Morning", "Afternoon", "Evening", "Night" };
    private static readonly int[]    PhaseBits   = { 1, 2, 4, 8 };               // DayPhaseMask values

    private const float RowH    = 22f;
    private const float Pad     = 3f;
    private const float BtnH    = 20f;

    // Colours for selected / deselected states
    private static readonly Color ColSelected   = new Color(0.25f, 0.60f, 1.00f, 1f);
    private static readonly Color ColDeselected = new Color(0.30f, 0.30f, 0.30f, 1f);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var locationProp = property.FindPropertyRelative("LocationScene");
        var spawnProp    = property.FindPropertyRelative("SpawnPointKey");
        var absentProp   = property.FindPropertyRelative("Absent");

        return RowH + Pad                                                          // days buttons
             + RowH + Pad                                                          // phase buttons
             + EditorGUI.GetPropertyHeight(locationProp, true) + Pad              // LocationScene (variable)
             + EditorGUI.GetPropertyHeight(spawnProp,    true) + Pad              // SpawnPointKey (variable)
             + EditorGUI.GetPropertyHeight(absentProp,   true);                   // Absent
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var daysProp     = property.FindPropertyRelative("Days");
        var phasesProp   = property.FindPropertyRelative("Phases");
        var locationProp = property.FindPropertyRelative("LocationScene");
        var spawnProp    = property.FindPropertyRelative("SpawnPointKey");
        var absentProp   = property.FindPropertyRelative("Absent");

        float x = position.x;
        float y = position.y;
        float w = position.width;

        // ── Days buttons ──────────────────────────────────────
        y = DrawToggleButtons(new Rect(x, y, w, RowH), daysProp, "Days", DayLabels, DayBits);
        y += Pad;

        // ── Phase buttons ─────────────────────────────────────
        y = DrawToggleButtons(new Rect(x, y, w, RowH), phasesProp, "Phases", PhaseLabels, PhaseBits);
        y += Pad;

        // ── Standard fields (use actual property height) ──────
        float locationH = EditorGUI.GetPropertyHeight(locationProp, true);
        EditorGUI.PropertyField(new Rect(x, y, w, locationH), locationProp, true);
        y += locationH + Pad;

        float spawnH = EditorGUI.GetPropertyHeight(spawnProp, true);
        EditorGUI.PropertyField(new Rect(x, y, w, spawnH), spawnProp, true);
        y += spawnH + Pad;

        float absentH = EditorGUI.GetPropertyHeight(absentProp, true);
        EditorGUI.PropertyField(new Rect(x, y, w, absentH), absentProp, true);

        EditorGUI.EndProperty();
    }

    private static float DrawToggleButtons(
        Rect rowRect, SerializedProperty maskProp, string rowLabel,
        string[] labels, int[] bits)
    {
        int count       = labels.Length;
        float labelW    = 50f;
        EditorGUI.LabelField(new Rect(rowRect.x, rowRect.y, labelW, rowRect.height), rowLabel);
        // space reserved for "Days"/"Phases" text
        float available = rowRect.width - labelW;
        float btnW      = available / count;

        float bx = rowRect.x + labelW;
        float by = rowRect.y + (rowRect.height - BtnH) * 0.5f;

        int current = maskProp.intValue;

        for (int i = 0; i < count; i++)
        {
            bool isOn   = (current & bits[i]) != 0;
            var  btnRect = new Rect(bx + i * btnW + 1, by, btnW - 2, BtnH);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = isOn ? ColSelected : ColDeselected;

            if (GUI.Button(btnRect, labels[i]))
            {
                if (isOn)
                    maskProp.intValue = current & ~bits[i];
                else
                    maskProp.intValue = current | bits[i];
            }

            GUI.backgroundColor = prev;
        }

        return rowRect.y + rowRect.height;
    }
}
#endif
