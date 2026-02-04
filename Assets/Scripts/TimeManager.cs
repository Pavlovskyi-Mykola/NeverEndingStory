using System;
using UnityEngine;

public enum DayPhase {Morning, Afternoon, Evening, Night}

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Current Time")]
    [SerializeField] private DayOfWeek dayOfWeek = DayOfWeek.Monday;
    [SerializeField] private DayPhase phase = DayPhase.Morning;

    public DayOfWeek DayOfWeek => dayOfWeek;
    public DayPhase Phase => phase;

    public event Action<DayOfWeek, DayPhase> OnTimeChanged;

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
            case DayPhase.Morning: phase = DayPhase.Afternoon; break;
            case DayPhase.Afternoon: phase = DayPhase.Evening; break;
            case DayPhase.Evening: phase = DayPhase.Night; break;
            case DayPhase.Night:
                phase = DayPhase.Morning;
                dayOfWeek = NextDay(dayOfWeek);
                break;
        }

        RaiseTimeChanged();
    }

    public void SetTime(DayOfWeek newDay, DayPhase newPhase)
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
}
