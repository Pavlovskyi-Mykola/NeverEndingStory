using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Small utility window for typing a new world-flag id.
/// Opened from the "+ Add new flag…" entry in FlagPickerDropdown.
/// </summary>
public sealed class NewFlagPromptWindow : EditorWindow
{
    private string _flagId = "";
    private Action<string> _onConfirm;
    private bool _focusSet;

    public static void Open(string initialValue, Action<string> onConfirm)
    {
        var window = CreateInstance<NewFlagPromptWindow>();
        window.titleContent = new GUIContent("New World Flag");
        window._onConfirm = onConfirm;
        window._flagId = initialValue ?? "";
        window.minSize = new Vector2(380f, 120f);
        window.maxSize = new Vector2(380f, 120f);
        window.ShowUtility();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Flag id (category.subject.state):", EditorStyles.miniBoldLabel);

        GUI.SetNextControlName("FlagIdField");
        _flagId = EditorGUILayout.TextField(_flagId);

        if (!_focusSet)
        {
            EditorGUI.FocusTextInControl("FlagIdField");
            _focusSet = true;
        }

        string trimmed = _flagId == null ? "" : _flagId.Trim();
        bool duplicate = WorldFlagEditorUtility.IsKnownFlag(trimmed);
        bool valid = trimmed.Length > 0 && !duplicate;

        if (duplicate)
            EditorGUILayout.HelpBox("This flag already exists in the FlagDatabase.", MessageType.Info);
        else if (trimmed.Length > 0 && trimmed.IndexOf('.') < 0)
            EditorGUILayout.HelpBox("Convention: category.subject.state (e.g. location.market.unlocked)", MessageType.None);

        GUILayout.FlexibleSpace();

        bool enterPressed = Event.current.type == EventType.KeyDown &&
                            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                Close();

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Add", GUILayout.Width(80)) || (valid && enterPressed))
                {
                    var confirm = _onConfirm;
                    Close();
                    confirm?.Invoke(trimmed);
                    GUIUtility.ExitGUI();
                }
            }
        }

        EditorGUILayout.Space(6);
    }
}
