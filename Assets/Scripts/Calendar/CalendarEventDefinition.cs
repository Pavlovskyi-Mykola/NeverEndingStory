using System;
using UnityEngine;

public enum CalendarRecurrence
{
    /// <summary>Fires on every checked day of the week (e.g. every Friday).</summary>
    Weekly = 0,
    /// <summary>Fires every N days counted from Start Day.</summary>
    EveryNDays = 1,
    /// <summary>Fires exactly once, on Start Day.</summary>
    OneTime = 2
}

[CreateAssetMenu(fileName = "CalendarEvent", menuName = "Game/Calendar/Calendar Event")]
public sealed class CalendarEventDefinition : ScriptableObject
{
    private const int PhasesPerDay = 4;

    [Header("Identity")]
    public string EventId;
    public string Title;
    [TextArea] public string Description;

    [Header("Schedule")]
    public CalendarRecurrence Recurrence = CalendarRecurrence.Weekly;

    [Tooltip("Phase of the day the event fires at.")]
    public TimeOfDay Phase = TimeOfDay.Morning;

    [Tooltip("Weekly only: which days of the week the event occurs on.")]
    public DayOfWeekMask DaysOfWeek = DayOfWeekMask.Friday;

    [Tooltip("EveryNDays only: interval in in-game days.")]
    [Min(1)] public int IntervalDays = 7;

    [Tooltip("EveryNDays: first day the event can occur (absolute day index, day 0 = game start). OneTime: THE day it occurs. Ignored for Weekly.")]
    [Min(0)] public int StartDay = 0;

    [Header("Skip behaviour")]
    [Tooltip("If the player sleeps past the event's phase, fire it anyway (late). Off = the event is missed and GameEvents.CalendarEventMissed is raised instead.")]
    public bool FireWhenSkipped = false;

    [Header("On fire (all optional)")]
    [Tooltip("World flag set when the event fires. Dialogue routing / quests can gate on it.")]
    [FlagId] public string SetFlagId;
    public bool SetFlagValue = true;

    [Tooltip("Quest started when the event fires (respects normal start rules/cooldowns).")]
    [QuestId] public string StartQuestId;

    [Tooltip("Show a toast when the event fires.")]
    public bool Notify = true;

    public bool IsValid() => !string.IsNullOrWhiteSpace(EventId);

    /// <summary>Does the event occur at some phase of the given absolute day?</summary>
    public bool OccursOnDay(long dayIndex, DayOfWeek dayOfWeek)
    {
        switch (Recurrence)
        {
            case CalendarRecurrence.Weekly:
                return (DaysOfWeek & DayOfWeekMaskExtensions.From(dayOfWeek)) != 0;

            case CalendarRecurrence.EveryNDays:
                return IntervalDays >= 1 && dayIndex >= StartDay && (dayIndex - StartDay) % IntervalDays == 0;

            case CalendarRecurrence.OneTime:
                return dayIndex == StartDay;

            default:
                return false;
        }
    }

    /// <summary>Does the event occur exactly at this absolute phase index?</summary>
    public bool OccursOnPhase(long phaseIndex, DayOfWeek dayOfWeek)
    {
        return (int)(phaseIndex % PhasesPerDay) == (int)Phase &&
               OccursOnDay(phaseIndex / PhasesPerDay, dayOfWeek);
    }
}
