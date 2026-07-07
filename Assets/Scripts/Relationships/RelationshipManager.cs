using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single-axis NPC relationship system: points accumulate into levels
/// (points / pointsPerLevel, clamped at maxLevel). Per-NPC tuning lives on
/// NpcDefinition; defaults here.
///
/// Anti-grind: gains are capped per NPC per in-game day (a "day" = 4 phases of
/// TimeManager.TotalPhasesElapsed). Losses are never capped.
///
/// Flag bridge: levels are mirrored into world flags 'rel.&lt;npcId&gt;.lvl&lt;N&gt;'
/// (set true up to the current level, false above it), so dialogue routing,
/// conditions, and quests can gate on relationships through the existing flag
/// systems with zero extra integration.
/// </summary>
public sealed class RelationshipManager : MonoBehaviour
{
    public static RelationshipManager Instance { get; private set; }
    public static event Action<RelationshipManager> InstanceReady;

    [Header("Defaults (NpcDefinition can override per NPC)")]
    [SerializeField] private int defaultPointsPerLevel = 10;
    [SerializeField] private int defaultMaxLevel = 5;

    [Header("Anti-grind")]
    [Tooltip("Max points a single NPC can gain per in-game day. 0 = uncapped.")]
    [SerializeField] private int dailyGainCap = 5;
    [Tooltip("Points granted for the first conversation with an NPC each day (counts toward the cap). 0 = off.")]
    [SerializeField] private int firstTalkOfDayPoints = 1;

    [Header("Flag bridge")]
    [Tooltip("Mirror levels into flags 'rel.<npcId>.lvl<N>' so flag-based systems react to relationships.")]
    [SerializeField] private bool syncLevelFlags = true;

    /// <summary>npcId whose relationship changed; null = everything (after load/reset).</summary>
    public event Action<string> OnRelationshipChanged;

    /// <summary>(npcId, oldLevel, newLevel)</summary>
    public event Action<string, int, int> OnLevelChanged;

    private const int PhasesPerDay = 4;

    [Serializable]
    private sealed class Entry
    {
        public int Points;
        public int GainedToday;
        public long LastGainDay = -1;
        public long LastTalkDay = -1;
    }

    private readonly Dictionary<string, Entry> _byNpc = new(StringComparer.Ordinal);
    private Dictionary<string, NpcDefinition> _npcDefs; // lazy, from NpcManager

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
    }

    private void OnEnable()
    {
        GameEvents.NpcTalked += HandleNpcTalked;
    }

    private void OnDisable()
    {
        GameEvents.NpcTalked -= HandleNpcTalked;
    }

    // -----------------------
    // Queries
    // -----------------------

    public int GetPoints(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return 0;
        return _byNpc.TryGetValue(npcId, out var entry) ? entry.Points : 0;
    }

    public int GetLevel(string npcId)
    {
        GetConfig(npcId, out int perLevel, out int maxLevel);
        return Mathf.Clamp(GetPoints(npcId) / perLevel, 0, maxLevel);
    }

    public int GetMaxLevel(string npcId)
    {
        GetConfig(npcId, out _, out int maxLevel);
        return maxLevel;
    }

    /// <summary>Progress within the current level, for UI bars (0..1; 1 at max level).</summary>
    public float GetLevelProgress(string npcId)
    {
        GetConfig(npcId, out int perLevel, out int maxLevel);

        int points = GetPoints(npcId);
        if (points >= perLevel * maxLevel) return 1f;

        return (points % perLevel) / (float)perLevel;
    }

    // -----------------------
    // Mutation
    // -----------------------

    /// <summary>
    /// Signed delta. Positive gains respect the per-day cap; losses always apply.
    /// Points clamp to [0, maxLevel * pointsPerLevel].
    /// </summary>
    public void AddPoints(string npcId, int amount)
    {
        if (string.IsNullOrEmpty(npcId) || amount == 0)
            return;

        GetConfig(npcId, out int perLevel, out int maxLevel);

        var entry = GetOrCreate(npcId);
        int oldLevel = Mathf.Clamp(entry.Points / perLevel, 0, maxLevel);

        if (amount > 0)
        {
            long today = CurrentDay();
            if (entry.LastGainDay != today)
            {
                entry.LastGainDay = today;
                entry.GainedToday = 0;
            }

            if (dailyGainCap > 0)
                amount = Mathf.Min(amount, dailyGainCap - entry.GainedToday);

            if (amount <= 0)
                return; // capped out for today

            entry.GainedToday += amount;
        }

        int maxPoints = perLevel * maxLevel;
        int next = Mathf.Clamp(entry.Points + amount, 0, maxPoints);
        if (next == entry.Points)
            return;

        entry.Points = next;

        int newLevel = Mathf.Clamp(entry.Points / perLevel, 0, maxLevel);

        if (newLevel != oldLevel)
        {
            SyncLevelFlags(npcId, newLevel, maxLevel);
            OnLevelChanged?.Invoke(npcId, oldLevel, newLevel);
        }

        OnRelationshipChanged?.Invoke(npcId);
    }

    private void HandleNpcTalked(string npcId, string dialogueId)
    {
        if (firstTalkOfDayPoints <= 0 || string.IsNullOrEmpty(npcId))
            return;

        var entry = GetOrCreate(npcId);
        long today = CurrentDay();
        if (entry.LastTalkDay == today)
            return;

        entry.LastTalkDay = today;
        AddPoints(npcId, firstTalkOfDayPoints);
    }

    // -----------------------
    // Internals
    // -----------------------

    private Entry GetOrCreate(string npcId)
    {
        if (!_byNpc.TryGetValue(npcId, out var entry))
        {
            entry = new Entry();
            _byNpc[npcId] = entry;
        }

        return entry;
    }

    private static long CurrentDay()
    {
        return TimeManager.Instance != null
            ? TimeManager.Instance.TotalPhasesElapsed / PhasesPerDay
            : 0;
    }

    private void GetConfig(string npcId, out int pointsPerLevel, out int maxLevel)
    {
        pointsPerLevel = Mathf.Max(1, defaultPointsPerLevel);
        maxLevel = Mathf.Max(1, defaultMaxLevel);

        var def = FindNpcDefinition(npcId);
        if (def == null) return;

        if (def.RelationshipPointsPerLevel > 0) pointsPerLevel = def.RelationshipPointsPerLevel;
        if (def.RelationshipMaxLevel > 0) maxLevel = def.RelationshipMaxLevel;
    }

    private NpcDefinition FindNpcDefinition(string npcId)
    {
        if (string.IsNullOrEmpty(npcId))
            return null;

        if (_npcDefs == null)
        {
            var nm = NpcManager.Instance;
            if (nm == null || nm.Npcs == null)
                return null; // don't cache an empty map before NpcManager exists

            _npcDefs = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < nm.Npcs.Count; i++)
            {
                var def = nm.Npcs[i];
                if (def != null && !string.IsNullOrWhiteSpace(def.NpcId))
                    _npcDefs[def.NpcId] = def;
            }
        }

        return _npcDefs.TryGetValue(npcId, out var found) ? found : null;
    }

    private void SyncLevelFlags(string npcId, int level, int maxLevel)
    {
        if (!syncLevelFlags) return;

        for (int l = 1; l <= maxLevel; l++)
            WorldFlags.Set($"rel.{npcId}.lvl{l}", l <= level);
    }

    // -----------------------
    // Save / Load
    // -----------------------

    [Serializable]
    public class Snapshot
    {
        public List<RelationshipEntrySave> entries = new();
    }

    [Serializable]
    public class RelationshipEntrySave
    {
        public string npcId;
        public int points;
        public int gainedToday;
        public long lastGainDay = -1;
        public long lastTalkDay = -1;
    }

    public Snapshot CaptureSnapshot()
    {
        var snap = new Snapshot();

        foreach (var kv in _byNpc)
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null)
                continue;

            snap.entries.Add(new RelationshipEntrySave
            {
                npcId = kv.Key,
                points = kv.Value.Points,
                gainedToday = kv.Value.GainedToday,
                lastGainDay = kv.Value.LastGainDay,
                lastTalkDay = kv.Value.LastTalkDay
            });
        }

        return snap;
    }

    public void RestoreSnapshot(Snapshot snap)
    {
        _byNpc.Clear();

        if (snap?.entries != null)
        {
            for (int i = 0; i < snap.entries.Count; i++)
            {
                var e = snap.entries[i];
                if (e == null || string.IsNullOrEmpty(e.npcId))
                    continue;

                _byNpc[e.npcId] = new Entry
                {
                    Points = Mathf.Max(0, e.points),
                    GainedToday = Mathf.Max(0, e.gainedToday),
                    LastGainDay = e.lastGainDay,
                    LastTalkDay = e.lastTalkDay
                };
            }
        }

        // Level flags come from the same save's WorldState, so they're already
        // consistent — but re-sync defensively (SetFlag no-ops when unchanged).
        foreach (var kv in _byNpc)
        {
            GetConfig(kv.Key, out int perLevel, out int maxLevel);
            SyncLevelFlags(kv.Key, Mathf.Clamp(kv.Value.Points / perLevel, 0, maxLevel), maxLevel);
        }

        OnRelationshipChanged?.Invoke(null); // null = everything changed
    }
}
