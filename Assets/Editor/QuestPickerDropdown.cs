using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public sealed class QuestPickerDropdown : AdvancedDropdown
{
    private readonly string _currentValue;
    private readonly string[] _ids;

    private sealed class QuestItem : AdvancedDropdownItem
    {
        public readonly string QuestId;
        public QuestItem(string name, string questId) : base(name) { QuestId = questId; }
    }

    private QuestPickerDropdown(AdvancedDropdownState state, string[] ids, string currentValue)
        : base(state)
    {
        _ids = ids ?? Array.Empty<string>();
        _currentValue = currentValue ?? string.Empty;
        minimumSize = new Vector2(320f, 360f);
    }

    public static void Show(Rect buttonRect, SerializedProperty property)
    {
        if (property == null) return;

        string propertyPath = property.propertyPath;
        SerializedObject so = property.serializedObject;

        var dropdown = new QuestPickerDropdown(
            new AdvancedDropdownState(),
            QuestIdEditorUtility.GetAllQuestIds(),
            property.stringValue);

        dropdown._apply = selectedId =>
        {
            if (so == null || so.targetObject == null) return;

            so.Update();
            var prop = so.FindProperty(propertyPath);
            if (prop != null)
            {
                prop.stringValue = selectedId;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
            }
        };

        dropdown.Show(buttonRect);
    }

    private Action<string> _apply;

    protected override AdvancedDropdownItem BuildRoot()
    {
        var root = new AdvancedDropdownItem("Quests");

        if (_ids.Length == 0)
        {
            root.AddChild(new AdvancedDropdownItem("No QuestDefinition assets found"));
            return root;
        }

        foreach (string id in _ids.OrderBy(i => i, StringComparer.Ordinal))
        {
            string title = QuestIdEditorUtility.GetTitle(id);
            string label = string.IsNullOrEmpty(title) || title == id ? id : $"{title}  ({id})";

            var item = new QuestItem(label, id);
            if (string.Equals(id, _currentValue, StringComparison.Ordinal))
                item.icon = EditorGUIUtility.IconContent("TestPassed").image as Texture2D;

            root.AddChild(item);
        }

        return root;
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        if (item is QuestItem questItem)
            _apply?.Invoke(questItem.QuestId);
    }
}
