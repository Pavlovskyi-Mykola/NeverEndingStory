#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ItemIdAttribute))]
public class ItemIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var database = FindDatabase();
        if (database == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var items = database.Items;
        if (items == null || items.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var options = new List<string> { "<None>" };
        int selectedIndex = 0;

        string currentValue = property.stringValue;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || !item.IsValid())
                continue;

            options.Add(string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.ItemId
                : $"{item.DisplayName} ({item.ItemId})");

            if (item.ItemId == currentValue)
                selectedIndex = options.Count - 1;
        }

        int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, options.ToArray());

        if (newIndex <= 0)
        {
            property.stringValue = string.Empty;
            return;
        }

        int validItemCounter = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || !item.IsValid())
                continue;

            validItemCounter++;
            if (validItemCounter == newIndex)
            {
                property.stringValue = item.ItemId;
                return;
            }
        }
    }

    private ItemDatabase FindDatabase()
    {
        if (ItemDatabaseHolder.TryGet(out var db))
            return db;

        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
        ItemDatabaseHolder.Set(db);
        return db;
    }

    private static class ItemDatabaseHolder
    {
        private static ItemDatabase _cached;

        public static bool TryGet(out ItemDatabase db)
        {
            db = _cached;
            return db != null;
        }

        public static void Set(ItemDatabase db)
        {
            _cached = db;
        }
    }
}
#endif