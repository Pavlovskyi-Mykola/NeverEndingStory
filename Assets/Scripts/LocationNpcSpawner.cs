using System;
using System.Collections.Generic;
using UnityEngine;

public class LocationNpcSpawner : MonoBehaviour
{
    [Serializable]
    public class SpawnPoint
    {
        public string Key;          // e.g. "Door", "Table01"
        public Transform Point;     // empty transform in scene
    }

    [Header("Location Identity")]
    [Tooltip("Scene reference for this location. Used to match NPC schedule entries.")]
    public SceneReference LocationScene;

    [Header("Spawn Points")]
    public List<SpawnPoint> SpawnPoints = new List<SpawnPoint>();

    // npcId -> instance
    private readonly Dictionary<string, GameObject> _spawned = new Dictionary<string, GameObject>();

    // spawnPointKey -> transform
    private Dictionary<string, Transform> _pointsByKey;

    private void Awake()
    {
        BuildSpawnPointLookup();
    }

    private void OnValidate()
    {
        // Helps keep it correct in editor
        BuildSpawnPointLookup();
    }

    private void BuildSpawnPointLookup()
    {
        _pointsByKey = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

        if (SpawnPoints == null) return;

        foreach (var sp in SpawnPoints)
        {
            if (sp == null) continue;
            if (string.IsNullOrWhiteSpace(sp.Key)) continue;
            if (sp.Point == null) continue;

            // If duplicates exist, first wins (prevents random overwrites)
            if (!_pointsByKey.ContainsKey(sp.Key))
                _pointsByKey.Add(sp.Key, sp.Point);
        }
    }

    public GameObject EnsureSpawned(string npcId, GameObject prefab, string spawnPointKey)
    {
        if (_spawned.TryGetValue(npcId, out var existing) && existing != null)
        {
            // Already spawned -> optionally move if spawn point changed
            var t = ResolveSpawnPoint(spawnPointKey);
            if (t != null)
            {
                existing.transform.SetPositionAndRotation(t.position, t.rotation);
                existing.transform.SetParent(t, worldPositionStays: true);
            }
            return existing;
        }

        // Spawn new
        var point = ResolveSpawnPoint(spawnPointKey);
        Vector3 pos = point != null ? point.position : transform.position;
        Quaternion rot = point != null ? point.rotation : transform.rotation;

        var instance = Instantiate(prefab, pos, rot, point != null ? point : transform);
        instance.name = $"{npcId} (NPC)";

        _spawned[npcId] = instance;
        return instance;
    }

    public void Despawn(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            return;

        if (_spawned.TryGetValue(npcId, out var go))
        {
            if (go != null)
                Destroy(go);

            _spawned.Remove(npcId);
        }
    }

    public void DespawnAll()
    {
        foreach (var kvp in _spawned)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        _spawned.Clear();
    }

    private Transform ResolveSpawnPoint(string spawnPointKey)
    {
        if (_pointsByKey == null) BuildSpawnPointLookup();

        if (string.IsNullOrWhiteSpace(spawnPointKey))
            return null;

        if (_pointsByKey != null && _pointsByKey.TryGetValue(spawnPointKey, out var t))
            return t;

        return null; // fall back handled by caller
    }
}
