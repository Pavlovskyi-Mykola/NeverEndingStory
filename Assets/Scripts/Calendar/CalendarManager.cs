using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fires scheduled calendar events (weekly meetings, every-N-days reviews,
/// one-time story dates) off TimeManager's absolute phase clock
/// (TotalPhasesElapsed, 4 phases per day).
///
/// Skips are handled: SleepToMorning can jump several phases in one TimeChanged,
/// so every phase in the gap is evaluated. An event whose phase was jumped over
/// fires late when FireWhenSkipped is set, otherwise it raises
/// GameEvents.CalendarEventMissed — sleeping through the board meeting has
/// consequences.
///
/// System-sourced time changes (save restore, resets, SetTime) never fire
/// events; they only re-anchor the clock, so loading a save can't replay a
/// week of meetings.
///
/// Flag bridge: each event mirrors "occurs today" into flag
/// 'evt.&lt;eventId&gt;.today', so dialogue routing and quests can react to event
/// days through the existing flag systems with zero extra integration.
/// </summary>
public sealed class CalendarManager : MonoBehaviour
{
    public static CalendarManager Instance { get; private set; }
    public static event Action<CalendarManager> InstanceReady;

    [Header("Data")]
    [SerializeField] private CalendarEventDatabase database;

    [Header("Flag bridge")]
    [Tooltip("Mirror 'this event occurs today' into flag 'evt.<eventId>.today'.")]
    [SerializeField] private bool syncTodayFlags = true;

    private const int PhasesPerDay = 4;

    private sealed class EventState
    {
        public long LastFiredPhase = -1;
        public int TimesFired;
    }

    private readonly Dictionary<string, EventState> _byEvent = new(StringComparer.Ordinal);

    // Last absolute phase already evaluated. -1 = nothing yet.
    private long _lastProcessedPhase = -1;

    public IReadOnlyList<CalendarEventDefinition> Events =>
        database != null && database.Events != null
            ? database.Events
            : (IReadOnlyList<CalendarEventDefinition>)Array.Empty<CalendarEventDefinition>();

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
            // Loads/resets/teleports re-anchor without firing.
            _lastProcessedPhase = now;
            SyncTodayFlags();
            return;
        }

        // First real tick without a prior anchor: evaluate the current phase only.
        if (_lastProcessedPhase < 0)
            _lastProcessedPhase = now - 1;

        if (now > _lastProcessedPhase)
        {
            ProcessPhases(_lastProcessedPhase + 1, now, day);
            _lastProcessedPhase = now;
        }

        SyncTodayFlags();
    }

    private void ProcessPhases(long from, long to, DayOfWeek currentDayOfWeek)
    {
        var events = database != null ? database.Events : null;
        if (events == null || events.Count == 0) return;

        long currentDayIndex = to / PhasesPerDay;

        for (long p = from; p <= to; p++)
        {
            var dow = DayOfWeekAt(p / PhasesPerDay, currentDayIndex, currentDayOfWeek);

            for (int i = 0; i < events.Count; i++)
            {
                var def = events[i];
                if (def == null || !def.IsValid()) continue;
                if (!def.OccursOnPhase(p, dow)) continue;

                var state = GetOrCreate(def.EventId);
                if (state.LastFiredPhase >= p) continue;

                bool onTime = p == to;
                if (!onTime && !def.FireWhenSkipped)
                {
                    GameEvents.RaiseCalendarEventMissed(def.EventId);
                    continue;
                }

                Fire(def, state, p);
            }
        }
    }

    private void Fire(CalendarEventDefinition def, EventState state, long phaseIndex)
    {
        state.LastFiredPhase = phaseIndex;
        state.TimesFired++;

        if (!string.IsNullOrWhiteSpace(def.SetFlagId))
            WorldFlags.Set(def.SetFlagId, def.SetFlagValue);

        if (!string.IsNullOrWhiteSpace(def.StartQuestId) && QuestManager.Instance != null)
            QuestManager.Instance.StartQuest(def.StartQuestId);

        if (def.Notify)
            Notifications.Post(
                string.IsNullOrWhiteSpace(def.Title) ? def.EventId : def.Title,
                def.Description,
                NotificationType.CalendarEvent);

        GameEvents.RaiseCalendarEventFired(def.EventId);
    }

    // -----------------------
    // Queries (UI / anticipation)
    // -----------------------

    public readonly struct UpcomingOccurrence
    {
        public readonly CalendarEventDefinition Event;
        public readonly long PhaseIndex;
        /// <summary>0 = later today, 1 = tomorrow, ...</summary>
        public readonly int DaysAway;
        public readonly DayOfWeek Day;
        public readonly TimeOfDay Phase;

        public UpcomingOccurrence(CalendarEventDefinition evt, long phaseIndex, int daysAway, DayOfWeek day, TimeOfDay phase)
        {
            Event = evt;
            PhaseIndex = phaseIndex;
            DaysAway = daysAway;
            Day = day;
            Phase = phase;
        }
    }

    /// <summary>
    /// Occurrences strictly after the current phase, soonest first.
    /// For a calendar/agenda UI ("Board meeting — Friday morning").
    /// </summary>
    public List<UpcomingOccurrence> GetUpcoming(int lookAheadDays = 7)
    {
        var result = new List<UpcomingOccurrence>();

        var tm = TimeManager.Instance;
        var events = database != null ? database.Events : null;
        if (tm == null || events == null || events.Count == 0)
            return result;

        long now = tm.TotalPhasesElapsed;
        long currentDayIndex = now / PhasesPerDay;
        long last = now + Mathf.Max(1, lookAheadDays) * PhasesPerDay;

        for (long p = now + 1; p <= last; p++)
        {
            long dayIndex = p / PhasesPerDay;
            var dow = DayOfWeekAt(dayIndex, currentDayIndex, tm.DayOfWeek);

            for (int i = 0; i < events.Count; i++)
            {
                var def = events[i];
                if (def == null || !def.IsValid()) continue;
                if (!def.OccursOnPhase(p, dow)) continue;

                // One-time events that already fired are history, not upcoming.
                if (def.Recurrence == CalendarRecurrence.OneTime &&
                    _byEvent.TryGetValue(def.EventId, out var state) && state.LastFiredPhase >= 0)
                    continue;

                result.Add(new UpcomingOccurrence(
                    def, p, (int)(dayIndex - currentDayIndex), dow, (TimeOfDay)(p % PhasesPerDay)));
            }
        }

        return result;
    }

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

    /// <summary>Day of week at an absolute day index, anchored on the current day/DayOfWeek pair.</summary>
    private static DayOfWeek DayOfWeekAt(long dayIndex, long currentDayIndex, DayOfWeek currentDayOfWeek)
    {
        long dow = ((int)currentDayOfWeek + (dayIndex - currentDayIndex)) % 7;
        if (dow < 0) dow += 7;
        return (DayOfWeek)dow;
    }

    private void SyncTodayFlags()
    {
        if (!syncTodayFlags) return;

        var tm = TimeManager.Instance;
        var events = database != null ? database.Events : null;
        if (tm == null || events == null) return;

        long dayIndex = tm.TotalPhasesElapsed / PhasesPerDay;

        for (int i = 0; i < events.Count; i++)
        {
            var def = events[i];
            if (def == null || !def.IsValid()) continue;

            WorldFlags.Set($"evt.{def.EventId}.today", def.OccursOnDay(dayIndex, tm.DayOfWeek));
        }
    }

    // -----------------------
    // Save / Load
    // -----------------------

    [Serializable]
    public class Snapshot
    {
        public List<EventStateSave> entries = new();
    }

    [Serializable]
    public class EventStateSave
    {
        public string eventId;
        public long lastFiredPhase = -1;
        public int timesFired;
    }

    public Snapshot CaptureSnapshot()
    {
        var snap = new Snapshot();

        foreach (var kv in _byEvent)
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null)
                continue;

            snap.entries.Add(new EventStateSave
            {
                eventId = kv.Key,
                lastFiredPhase = kv.Value.LastFiredPhase,
                timesFired = kv.Value.TimesFired
            });
        }

        return snap;
    }

    public void RestoreSnapshot(Snapshot snap)
    {
        _byEvent.Clear();

        if (snap?.entries != null)
        {
            for (int i = 0; i < snap.entries.Count; i++)
            {
                var e = snap.entries[i];
                if (e == null || string.IsNullOrEmpty(e.eventId))
                    continue;

                _byEvent[e.eventId] = new EventState
                {
                    LastFiredPhase = e.lastFiredPhase,
                    TimesFired = Mathf.Max(0, e.timesFired)
                };
            }
        }

        // Anchor to the restored clock so the next real phase advance evaluates
        // exactly one new phase; never persisted because it must always equal the
        // save's TotalPhasesElapsed anyway.
        _lastProcessedPhase = TimeManager.Instance != null ? TimeManager.Instance.TotalPhasesElapsed : -1;

        SyncTodayFlags();
    }
}
