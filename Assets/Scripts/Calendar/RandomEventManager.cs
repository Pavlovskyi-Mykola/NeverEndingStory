using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small variety layer over the calendar: on each phase advance there is a
/// chance a random flavour event fires ("a colleague asks a favor", "market
/// shifts"), picked by weight from the eligible pool.
///
/// Rolls are deterministic: a persisted run seed is mixed with the absolute
/// phase index, so reloading a save and advancing the same phase produces the
/// same outcome — no save-scumming a bad market day away.
///
/// Unlike CalendarManager, phases jumped over by sleeping are NOT back-filled:
/// scheduled appointments happen (or are missed) while you sleep, but flavour
/// moments only occur in phases the player actually lives through.
/// </summary>
public sealed class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }
    public static event Action<RandomEventManager> InstanceReady;

    [Header("Data")]
    [SerializeField] private RandomEventDatabase database;

    [Header("Tuning")]
    [Tooltip("Chance that a random event fires on any given phase advance.")]
    [Range(0f, 1f)]
    [SerializeField] private float eventChancePerPhase = 0.2f;

    [Tooltip("Hard cap of random events per in-game day.")]
    [Min(1)]
    [SerializeField] private int maxEventsPerDay = 1;

    [Tooltip("Skip rolls on days that have a scheduled calendar event, so board-meeting days stay focused.")]
    [SerializeField] private bool suppressOnCalendarEventDays = false;

    /// <summary>Raised after a random event's effects were applied. (eventId)</summary>
    public event Action<string> OnRandomEventFired;

    private const int PhasesPerDay = 4;

    private sealed class EventState
    {
        public long LastFiredDay = -1;
        public int TimesFired;
    }

    private readonly Dictionary<string, EventState> _byEvent = new(StringComparer.Ordinal);

    // Run seed for deterministic rolls; 0 = not generated yet (lazy, persisted).
    private int _seed;

    // Highest absolute phase already rolled. -1 = nothing yet. Like the
    // calendar, System time changes only re-anchor this.
    private long _lastRolledPhase = -1;

    // Per-day cap bookkeeping (persisted so a mid-day save can't reset the cap).
    private long _firedTodayDay = -1;
    private int _firedTodayCount;

    // Effects can raise StatsChanged/FlagChanged storms; block re-entrant rolls.
    private bool _firing;

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
        GameEvents.TimeChanged += HandleTimeChanged;
    }

    private void OnDisable()
    {
        GameEvents.TimeChanged -= HandleTimeChanged;
    }

    // -----------------------
    // Time handling
    // -----------------------

    private void HandleTimeChanged(DayOfWeek day, TimeOfDay phase, TimeChangeSource source)
    {
        var tm = TimeManager.Instance;
        if (tm == null) return;

        long now = tm.TotalPhasesElapsed;

        if (source == TimeChangeSource.System)
        {
            _lastRolledPhase = now;
            return;
        }

        if (_firing)
            return;

        if (now <= _lastRolledPhase)
            return;

        // Skipped phases are consumed unrolled by design (see class summary).
        _lastRolledPhase = now;

        RollPhase(now, day, phase);
    }

    private void RollPhase(long phaseIndex, DayOfWeek dayOfWeek, TimeOfDay phase)
    {
        var events = database != null ? database.Events : null;
        if (events == null || events.Count == 0)
            return;

        long dayIndex = phaseIndex / PhasesPerDay;

        if (_firedTodayDay != dayIndex)
        {
            _firedTodayDay = dayIndex;
            _firedTodayCount = 0;
        }

        if (_firedTodayCount >= maxEventsPerDay)
            return;

        if (suppressOnCalendarEventDays && HasCalendarEventToday(dayIndex, dayOfWeek))
            return;

        var rng = new System.Random(MixSeed(GetOrCreateSeed(), phaseIndex));

        if (rng.NextDouble() >= eventChancePerPhase)
            return;

        var def = PickWeighted(events, dayIndex, dayOfWeek, phase, rng);
        if (def == null)
            return;

        Fire(def, dayIndex);
    }

    private RandomEventDefinition PickWeighted(
        List<RandomEventDefinition> events, long dayIndex, DayOfWeek dayOfWeek, TimeOfDay phase, System.Random rng)
    {
        var candidates = new List<RandomEventDefinition>();
        float totalWeight = 0f;

        for (int i = 0; i < events.Count; i++)
        {
            var def = events[i];
            if (def == null || !def.IsValid()) continue;
            if (!def.IsEligible(dayIndex, dayOfWeek, phase)) continue;

            if (_byEvent.TryGetValue(def.EventId, out var state))
            {
                if (def.OneTime && state.TimesFired > 0)
                    continue;

                if (def.CooldownDays > 0 && state.LastFiredDay >= 0 &&
                    dayIndex - state.LastFiredDay < def.CooldownDays)
                    continue;
            }

            candidates.Add(def);
            totalWeight += def.Weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
            return null;

        float roll = (float)(rng.NextDouble() * totalWeight);

        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].Weight;
            if (roll < 0f)
                return candidates[i];
        }

        return candidates[candidates.Count - 1]; // float edge: roll landed on totalWeight
    }

    private void Fire(RandomEventDefinition def, long dayIndex)
    {
        var state = GetOrCreate(def.EventId);
        state.LastFiredDay = dayIndex;
        state.TimesFired++;
        _firedTodayCount++;

        _firing = true;
        try
        {
            def.Effects?.Grant();

            if (!string.IsNullOrWhiteSpace(def.StartQuestId) && QuestManager.Instance != null)
                QuestManager.Instance.StartQuest(def.StartQuestId);
        }
        finally
        {
            _firing = false;
        }

        if (def.Notify)
            Notifications.Post(
                string.IsNullOrWhiteSpace(def.Title) ? def.EventId : def.Title,
                def.Description,
                NotificationType.RandomEvent);

        GameEvents.RaiseRandomEventFired(def.EventId);
        OnRandomEventFired?.Invoke(def.EventId);
    }

    private static bool HasCalendarEventToday(long dayIndex, DayOfWeek dayOfWeek)
    {
        var calendar = CalendarManager.Instance;
        if (calendar == null) return false;

        var events = calendar.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var def = events[i];
            if (def != null && def.IsValid() && def.OccursOnDay(dayIndex, dayOfWeek))
                return true;
        }

        return false;
    }

    // -----------------------
    // Queries
    // -----------------------

    public int GetTimesFired(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return 0;
        return _byEvent.TryGetValue(eventId, out var state) ? state.TimesFired : 0;
    }

    public bool HasFired(string eventId) => GetTimesFired(eventId) > 0;

    // -----------------------
    // Internals
    // -----------------------

    private EventState GetOrCreate(string eventId)
    {
        if (!_byEvent.TryGetValue(eventId, out var state))
        {
            state = new EventState();
            _byEvent[eventId] = state;
        }

        return state;
    }

    private int GetOrCreateSeed()
    {
        if (_seed == 0)
            _seed = Guid.NewGuid().GetHashCode() | 1; // |1 keeps it from ever being 0

        return _seed;
    }

    private static int MixSeed(int seed, long phaseIndex)
    {
        // Cheap avalanche mix so consecutive phases don't produce correlated rolls.
        ulong x = (ulong)seed ^ ((ulong)phaseIndex * 0x9E3779B97F4A7C15UL);
        x ^= x >> 33;
        x *= 0xFF51AFD7ED558CCDUL;
        x ^= x >> 33;
        return unchecked((int)x);
    }

    // -----------------------
    // Save / Load
    // -----------------------

    [Serializable]
    public class Snapshot
    {
        public int seed;
        public long firedTodayDay = -1;
        public int firedTodayCount;
        public List<EventStateSave> entries = new();
    }

    [Serializable]
    public class EventStateSave
    {
        public string eventId;
        public long lastFiredDay = -1;
        public int timesFired;
    }

    public Snapshot CaptureSnapshot()
    {
        var snap = new Snapshot
        {
            seed = _seed,
            firedTodayDay = _firedTodayDay,
            firedTodayCount = _firedTodayCount
        };

        foreach (var kv in _byEvent)
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null)
                continue;

            snap.entries.Add(new EventStateSave
            {
                eventId = kv.Key,
                lastFiredDay = kv.Value.LastFiredDay,
                timesFired = kv.Value.TimesFired
            });
        }

        return snap;
    }

    public void RestoreSnapshot(Snapshot snap)
    {
        _byEvent.Clear();

        _seed = snap?.seed ?? 0;
        _firedTodayDay = snap?.firedTodayDay ?? -1;
        _firedTodayCount = Mathf.Max(0, snap?.firedTodayCount ?? 0);

        if (snap?.entries != null)
        {
            for (int i = 0; i < snap.entries.Count; i++)
            {
                var e = snap.entries[i];
                if (e == null || string.IsNullOrEmpty(e.eventId))
                    continue;

                _byEvent[e.eventId] = new EventState
                {
                    LastFiredDay = e.lastFiredDay,
                    TimesFired = Mathf.Max(0, e.timesFired)
                };
            }
        }

        // Anchor like the calendar: the save's clock is the last rolled phase.
        _lastRolledPhase = TimeManager.Instance != null ? TimeManager.Instance.TotalPhasesElapsed : -1;
    }
}
