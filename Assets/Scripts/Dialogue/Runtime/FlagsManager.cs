using System.Collections.Generic;
using UnityEngine;

public class FlagsManager : MonoBehaviour
{
    public static FlagsManager Instance { get; private set; }

    [SerializeField] private List<string> debugTrueFlags = new();

    private readonly Dictionary<string, bool> _flags = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Optional: seed debug flags for testing
        for (int i = 0; i < debugTrueFlags.Count; i++)
            _flags[debugTrueFlags[i]] = true;
    }

    public bool GetFlag(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return _flags.TryGetValue(id, out var v) && v;
    }

    public void SetFlag(string id, bool value)
    {
        if (string.IsNullOrEmpty(id)) return;
        _flags[id] = value;
    }

    public void ClearAll() => _flags.Clear();
}
