using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Debug")]
    [SerializeField] private bool logChanges = false;

    private readonly Dictionary<string, int> _items = new Dictionary<string, int>(StringComparer.Ordinal);

    public event Action OnInventoryChanged;

    public static event Action<InventoryManager> InstanceReady;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InstanceReady?.Invoke(this);
        RaiseChanged();
    }

    public ItemDatabase Database => itemDatabase;

    public InventorySave CaptureState()
    {
        var save = new InventorySave();

        foreach (var kv in _items)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            if (kv.Value <= 0) continue;

            save.items.Add(new InventoryEntrySave
            {
                itemId = kv.Key,
                count = kv.Value
            });
        }

        return save;
    }

    public void RestoreState(InventorySave data)
    {
        _items.Clear();

        if (data != null && data.items != null)
        {
            for (int i = 0; i < data.items.Count; i++)
            {
                var e = data.items[i];
                if (e == null) continue;
                if (string.IsNullOrWhiteSpace(e.itemId)) continue;
                if (e.count <= 0) continue;

                int count = e.count;
                if (TryGetDefinition(e.itemId, out var def) && !def.Stackable)
                    count = 1;

                _items[e.itemId] = count;
            }
        }

        RaiseChanged();
    }

    public bool HasItem(string itemId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (count <= 0) count = 1;

        return _items.TryGetValue(itemId, out var owned) && owned >= count;
    }

    public int GetCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;
        return _items.TryGetValue(itemId, out var count) ? count : 0;
    }

    public void AddItem(string itemId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (count <= 0) return;

        _items.TryGetValue(itemId, out var oldCount);
        int newCount = oldCount + count;

        if (TryGetDefinition(itemId, out var def) && !def.Stackable)
        {
            if (oldCount >= 1)
            {
                Debug.LogWarning($"[Inventory] Ignored add of non-stackable '{itemId}' (already owned)");
                return;
            }

            newCount = 1;
        }

        _items[itemId] = newCount;

        if (logChanges)
            Debug.Log($"[Inventory] Added {count} x '{itemId}' (now {newCount})");

        RaiseChanged(itemId, oldCount, newCount);
    }

    public bool RemoveItem(string itemId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (count <= 0) return true;
        if (!_items.TryGetValue(itemId, out var oldCount)) return false;
        if (oldCount < count) return false;

        int newCount = oldCount - count;

        if (newCount <= 0)
            _items.Remove(itemId);
        else
            _items[itemId] = newCount;

        if (logChanges)
            Debug.Log($"[Inventory] Removed {count} x '{itemId}' (now {Mathf.Max(0, newCount)})");

        RaiseChanged(itemId, oldCount, Mathf.Max(0, newCount));
        return true;
    }

    public bool TryConsume(string itemId, int count = 1)
    {
        return RemoveItem(itemId, count);
    }

    public bool TryGetDefinition(string itemId, out ItemDefinition item)
    {
        item = null;
        return itemDatabase != null && itemDatabase.TryGet(itemId, out item);
    }

    public IReadOnlyDictionary<string, int> GetAll()
    {
        return _items;
    }

    private void RaiseChanged(string itemId = null, int oldCount = 0, int newCount = 0)
    {
        OnInventoryChanged?.Invoke();
        GameEvents.RaiseInventoryChanged(itemId, oldCount, newCount, this);
    }
}