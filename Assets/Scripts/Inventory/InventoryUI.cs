using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("List")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private InventoryRowUI rowPrefab;

    private readonly List<InventoryRowUI> _spawnedRows = new List<InventoryRowUI>();

    private void OnEnable()
    {
        GameEvents.InventoryChanged += HandleInventoryChanged;
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.InventoryChanged -= HandleInventoryChanged;
    }

    public void TogglePanel()
    {
        if (panelRoot == null) return;

        bool next = !panelRoot.activeSelf;
        panelRoot.SetActive(next);

        if (next)
            Refresh();
    }

    public void ShowPanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        Refresh();
    }

    public void HidePanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
    }

    private void HandleInventoryChanged(GameEvents.InventoryChange change)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (contentRoot == null || rowPrefab == null)
            return;

        ClearRows();

        var inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        var all = inventory.GetAll();
        if (all == null)
            return;

        foreach (var kv in all)
        {
            if (kv.Value <= 0)
                continue;

            ItemDefinition def = null;
            inventory.TryGetDefinition(kv.Key, out def);

            var row = Instantiate(rowPrefab, contentRoot);
            row.Bind(def, kv.Value);
            _spawnedRows.Add(row);
        }
    }

    private void ClearRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i].gameObject);
        }

        _spawnedRows.Clear();
    }
}