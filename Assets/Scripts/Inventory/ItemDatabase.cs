using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Inventory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    private Dictionary<string, ItemDefinition> _byId;

    private void OnEnable() => RebuildCache();

#if UNITY_EDITOR
    private void OnValidate() => RebuildCache();
#endif

    private void RebuildCache()
    {
        _byId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || !item.IsValid())
                continue;

            _byId[item.ItemId] = item;
        }
    }

    public bool TryGet(string itemId, out ItemDefinition item)
    {
        item = null;

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (_byId == null)
            RebuildCache();

        return _byId.TryGetValue(itemId, out item);
    }

    public ItemDefinition Get(string itemId)
    {
        TryGet(itemId, out var item);
        return item;
    }

    public IReadOnlyList<ItemDefinition> Items => items;
}