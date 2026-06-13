using System;
using UnityEngine;

public enum TimeOfDay {Morning, Afternoon, Evening, Night}

public enum TimeChangeSource {PlayerUI, Action, Dialogue, Quest, System }

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }
    public static event System.Action<TimeManager> InstanceReady;


    [Header("Current Time")]
    [SerializeField] private DayOfWeek dayOfWeek = DayOfWeek.Monday;
    [SerializeField] private TimeOfDay timeOfDay = TimeOfDay.Morning;

    // Monotonic count of phases since the game began. DayOfWeek wraps at 7, so it
    // can't measure elapsed game time on its own — this gives quests an absolute
    // clock for cooldowns/deadlines. Persisted in saves.
    [SerializeField] private long totalPhasesElapsed = 0;

    public DayOfWeek DayOfWeek => dayOfWeek;
    public TimeOfDay TimeOfDay => timeOfDay;
    public long TotalPhasesElapsed => totalPhasesElapsed;

    public event Action<DayOfWeek, TimeOfDay> OnTimeChanged;

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

    private void Start()
    {
        // Push initial state to UI/listeners
        RaiseTimeChanged(TimeChangeSource.System);
    }

    public TimeSave CaptureState()
    {
        return new TimeSave
        {
            dayOfWeek = (int)dayOfWeek,
            timeOfDay = (int)timeOfDay,
            totalPhasesElapsed = totalPhasesElapsed
        };
    }

    public void RestoreState(TimeSave data)
    {
        if (data == null) return;

        dayOfWeek = (DayOfWeek)Mathf.Clamp(data.dayOfWeek, 0, 6);
        timeOfDay = (TimeOfDay)Mathf.Clamp(data.timeOfDay, 0, 3);
        totalPhasesElapsed = data.totalPhasesElapsed >= 0 ? data.totalPhasesElapsed : 0;

        RaiseTimeChanged(TimeChangeSource.System);
    }

    public void AdvancePhase(TimeChangeSource source)
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.IsInDialogue &&
            source == TimeChangeSource.PlayerUI)
            return;

        switch (timeOfDay)
        {
            case TimeOfDay.Morning: timeOfDay = TimeOfDay.Afternoon; break;
            case TimeOfDay.Afternoon: timeOfDay = TimeOfDay.Evening; break;
            case TimeOfDay.Evening: timeOfDay = TimeOfDay.Night; break;
            case TimeOfDay.Night:
                timeOfDay = TimeOfDay.Morning;
                dayOfWeek = NextDay(dayOfWeek);
                break;
        }

        totalPhasesElapsed++;
        RaiseTimeChanged(source);
    }

    public void SetTime(DayOfWeek newDay, TimeOfDay newPhase)
    {
        dayOfWeek = newDay;
        timeOfDay = newPhase;
        RaiseTimeChanged(TimeChangeSource.System);
    }

    private void RaiseTimeChanged(TimeChangeSource source)
    {
        OnTimeChanged?.Invoke(dayOfWeek, timeOfDay);
        GameEvents.RaiseTimeChanged(dayOfWeek, timeOfDay, source);
    }

    private static DayOfWeek NextDay(DayOfWeek day)
    {
        // DayOfWeek: Sunday=0 ... Saturday=6
        int next = ((int)day + 1) % 7;
        return (DayOfWeek)next;
    }

    public void SleepToMorning(TimeChangeSource source)
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.IsInDialogue &&
            source == TimeChangeSource.PlayerUI)
            return;

        // Sleep ends the day -> next day morning. Phases skipped depend on the
        // current phase (Morning = a full 4-phase day, Night = 1).
        totalPhasesElapsed += 4 - (int)timeOfDay;

        timeOfDay = TimeOfDay.Morning;
        dayOfWeek = NextDay(dayOfWeek);

        RaiseTimeChanged(source);
    }
}
