using System;
using UnityEngine;

public enum TimeOfDay {Morning, Afternoon, Evening, Night}

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }
    public static event System.Action<TimeManager> InstanceReady;


    [Header("Current Time")]
    [SerializeField] private DayOfWeek dayOfWeek = DayOfWeek.Monday;
    [SerializeField] private TimeOfDay timeOfDay = TimeOfDay.Morning;

    public DayOfWeek DayOfWeek => dayOfWeek;
    public TimeOfDay TimeOfDay => timeOfDay;

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
        RaiseTimeChanged();
    }

    public void AdvancePhase()
    {
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

        RaiseTimeChanged();
    }

    public void SetTime(DayOfWeek newDay, TimeOfDay newPhase)
    {
        dayOfWeek = newDay;
        timeOfDay = newPhase;
        RaiseTimeChanged();
    }

    private void RaiseTimeChanged()
    {
        OnTimeChanged?.Invoke(dayOfWeek, timeOfDay);
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
        timeOfDay = TimeOfDay.Morning;
        dayOfWeek = NextDay(dayOfWeek);

        RaiseTimeChanged();
    }


}
