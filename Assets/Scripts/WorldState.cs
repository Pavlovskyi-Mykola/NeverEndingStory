using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }

    // Simple boolean flags
    private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>(StringComparer.Ordinal);

    // Optional: for debugging in Inspector
    [SerializeField] private bool logFlagChanges = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public WorldSave CaptureState()
    {
        var save = new WorldSave();

        foreach (var kv in _flags)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;

            save.flags.Add(new FlagEntrySave
            {
                key = kv.Key,
                value = kv.Value
            });
        }

        return save;
    }

    public void RestoreState(WorldSave data)
    {
        _flags.Clear();

        if (data != null && data.flags != null)
        {
            for (int i = 0; i < data.flags.Count; i++)
            {
                var e = data.flags[i];
                if (e == null) continue;
                if (string.IsNullOrWhiteSpace(e.key)) continue;

                _flags[e.key] = e.value;
            }
        }
    }

    public void ClearAllFlags()
    {
        _flags.Clear();
    }

    public bool HasFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return _flags.TryGetValue(key, out var v) && v;
    }

    public bool GetFlag(string key, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return _flags.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void SetFlag(string key, bool value = true)
    {
        if (string.IsNullOrEmpty(key)) return;

        bool had = _flags.TryGetValue(key, out var oldValue);
        if (had && oldValue == value) return;

#if UNITY_EDITOR
        if (!WorldFlags.IsKnown(key))
            Debug.LogWarning($"[WorldState] Setting flag '{key}' which is not declared in WorldFlags.cs — possible typo in dialogue/quest data.");
#endif

        _flags[key] = value;

        if (logFlagChanges)
            Debug.Log($"[WorldState] Flag '{key}' = {value}");

        GameEvents.RaiseFlagChanged(key, value);
    }

    public void ClearFlag(string key) => SetFlag(key, false);
}