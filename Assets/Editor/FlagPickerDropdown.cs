using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public sealed class FlagPickerDropdown : AdvancedDropdown
{
    private readonly Action<string> _onSelected;
    private readonly string _currentValue;
    private readonly string[] _flags;

    private sealed class FlagItem : AdvancedDropdownItem
    {
        public readonly string FlagId;

        public FlagItem(string name, string flagId) : base(name)
        {
            FlagId = flagId;
        }
    }

    private FlagPickerDropdown(AdvancedDropdownState state, string[] flags, string currentValue, Action<string> onSelected)
        : base(state)
    {
        _flags = flags ?? Array.Empty<string>();
        _currentValue = currentValue ?? string.Empty;
        _onSelected = onSelected;

        minimumSize = new Vector2(320f, 360f);
    }

    public static void Show(Rect buttonRect, SerializedProperty property)
    {
        if (property == null) return;

        string propertyPath = property.propertyPath;
        SerializedObject so = property.serializedObject;
        string currentValue = property.stringValue;

        var state = new AdvancedDropdownState();
        var dropdown = new FlagPickerDropdown(state, WorldFlagEditorUtility.GetAllFlags(), currentValue, selectedFlag =>
        {
            if (so == null || so.targetObject == null)
                return;

            so.Update();

            var prop = so.FindProperty(propertyPath);
            if (prop != null)
            {
                prop.stringValue = selectedFlag;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
            }
        });

        dropdown.Show(buttonRect);
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        var root = new AdvancedDropdownItem("World Flags");

        if (_flags.Length == 0)
        {
            root.AddChild(new AdvancedDropdownItem("No flags found in WorldFlags.cs"));
            return root;
        }

        var categoryMap = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);

        foreach (string flag in _flags.OrderBy(f => f, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(flag))
                continue;

            string[] parts = flag.Split('.');
            AdvancedDropdownItem parent = root;

            // Group by everything except the final part if possible.
            int groupCount = Mathf.Max(1, parts.Length - 1);

            string runningKey = string.Empty;
            for (int i = 0; i < groupCount; i++)
            {
                string part = parts[i];
                runningKey = string.IsNullOrEmpty(runningKey) ? part : runningKey + "." + part;

                if (!categoryMap.TryGetValue(runningKey, out var group))
                {
                    group = new AdvancedDropdownItem(part);
                    categoryMap[runningKey] = group;
                    parent.AddChild(group);
                }

                parent = group;
            }

            string leafLabel = parts.Length > 0 ? parts[parts.Length - 1] : flag;
            var item = new FlagItem(leafLabel, flag);

            if (string.Equals(flag, _currentValue, StringComparison.Ordinal))
                item.icon = EditorGUIUtility.IconContent("TestPassed").image as Texture2D;

            parent.AddChild(item);
        }

        return root;
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        if (item is FlagItem flagItem)
            _onSelected?.Invoke(flagItem.FlagId);
    }
}