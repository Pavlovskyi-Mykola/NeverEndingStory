using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RandomEvent", menuName = "Game/Calendar/Random Event")]
public sealed class RandomEventDefinition : ScriptableObject
{
    [Header("Identity")]
    public string EventId;
    public string Title;
    [TextArea] public string Description;

    [Header("Selection")]
    [Tooltip("Relative chance among eligible events (2 = twice as likely as 1). 0 disables the event.")]
    [Min(0f)] public float Weight = 1f;

    [Header("Eligibility")]
    public DayOfWeekMask AllowedDays = DayOfWeekMask.All;
    public DayPhaseMask AllowedPhases = DayPhaseMask.All;

    [Tooltip("Earliest absolute day (0 = game start) the event can occur. Use to keep late-game events out of week one.")]
    [Min(0)] public int EarliestDay = 0;

    [Tooltip("Minimum days between two occurrences of THIS event. 0 = no cooldown.")]
    [Min(0)] public int CooldownDays = 0;

    [Tooltip("Fire at most once per playthrough.")]
    public bool OneTime = false;

    [Tooltip("Optional world-flag gate.")]
    [FlagId] public string RequiredFlagId;
    public bool RequiredFlagValue = true;

    [Header("On fire (all optional)")]
    [Tooltip("Stat deltas (negatives allowed — 'market shifts: -50 money'), flags, energy. Leave AdvanceTimePhase off: events fire from a time change already.")]
    public QuestRewardDefinition Effects = new();

    [Tooltip("Quest started when the event fires (respects normal start rules/cooldowns).")]
    [QuestId] public string StartQuestId;

    [Tooltip("Show a toast when the event fires.")]
    public bool Notify = true;

    public bool IsValid() => !string.IsNullOrWhiteSpace(EventId) && Weight > 0f;

    public bool IsEligible(long dayIndex, DayOfWeek dayOfWeek, TimeOfDay phase)
    {
        if (dayIndex < EarliestDay)
            return false;

        if ((AllowedDays & DayOfWeekMaskExtensions.From(dayOfWeek)) == 0)
            return false;

        if ((AllowedPhases & DayPhaseMaskExtensions.From(phase)) == 0)
            return false;

        if (!string.IsNullOrWhiteSpace(RequiredFlagId) &&
            WorldFlags.Get(RequiredFlagId) != RequiredFlagValue)
            return false;

        return true;
    }
}
