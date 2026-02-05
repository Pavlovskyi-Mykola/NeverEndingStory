using System;
using UnityEngine;

public enum TimeOfDay {Morning, Afternoon, Evening, Night}

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Current Time")]
    [SerializeField] private DayOfWeek dayOfWeek = DayOfWeek.Monday;
    [SerializeField] private TimeOfDay phase = TimeOfDay.Morning;

    public DayOfWeek DayOfWeek => dayOfWeek;
    public TimeOfDay Phase => phase;

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
    }

    private void Start()
    {
        // Push initial state to UI/listeners
        RaiseTimeChanged();
    }

    public void AdvancePhase()
    {
        switch (phase)
        {
            case TimeOfDay.Morning: phase = TimeOfDay.Afternoon; break;
            case TimeOfDay.Afternoon: phase = TimeOfDay.Evening; break;
            case TimeOfDay.Evening: phase = TimeOfDay.Night; break;
            case TimeOfDay.Night:
                phase = TimeOfDay.Morning;
                dayOfWeek = NextDay(dayOfWeek);
                break;
        }

        RaiseTimeChanged();
    }

    public void SetTime(DayOfWeek newDay, TimeOfDay newPhase)
    {
        dayOfWeek = newDay;
        phase = newPhase;
        RaiseTimeChanged();
    }

    private void RaiseTimeChanged()
    {
        OnTimeChanged?.Invoke(dayOfWeek, phase);
    }

    private static DayOfWeek NextDay(DayOfWeek day)
    {
        // DayOfWeek: Sunday=0 ... Saturday=6
        int next = ((int)day + 1) % 7;
        return (DayOfWeek)next;
    }

    public void SleepToMorning()
    {
        // Sleep ends the day -> next day morning
        phase = TimeOfDay.Morning;
        dayOfWeek = NextDay(dayOfWeek);

        RaiseTimeChanged();
    }

}
