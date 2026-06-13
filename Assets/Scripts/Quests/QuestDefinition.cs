using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Quest", menuName = "Game/Quests/Quest Definition")]
public sealed class QuestDefinition : ScriptableObject
{
    [Header("Identity")]
    public string QuestId;

    [Header("Display")]
    public string Title;
    [TextArea(2, 6)] public string Description;

    [Header("Steps (linear)")]
    public List<QuestStepDefinition> Steps = new();

    [Header("Completion Rewards")]
    public List<QuestRewardDefinition> CompletionRewards = new();

    [Header("Auto-Start")]
    [Tooltip("When true, QuestManager starts this quest automatically once Start Condition is met (no dialogue command needed).")]
    public bool AutoStart = false;
    public QuestStartCondition StartCondition = new();

    [Header("Repeatable")]
    [Tooltip("When true, the quest can be started again after completion (e.g. pay rent each week).")]
    public bool Repeatable = false;
    [Tooltip("Phases that must pass after completion before it can restart. 0 = next time its start condition is re-evaluated. There are 4 phases per day.")]
    public int RepeatCooldownPhases = 0;

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(QuestId)) return false;
        if (Steps == null || Steps.Count == 0) return false;
        return true;
    }
}

[Serializable]
public sealed class QuestStartCondition
{
    [Tooltip("All of these quests must be completed.")]
    [QuestId] public List<string> RequireCompletedQuests = new();

    [Tooltip("All of these quests must be currently active.")]
    [QuestId] public List<string> RequireActiveQuests = new();

    [Tooltip("Optional world flag gate.")]
    [FlagId] public string RequireFlagId;
    public bool RequireFlagValue = true;

    [Header("Time window (optional)")]
    public bool RestrictByDay = false;
    public DayOfWeekMask AllowedDays = DayOfWeekMask.All;
    public bool RestrictByPhase = false;
    public DayPhaseMask AllowedPhases = DayPhaseMask.All;

    public bool IsMet(QuestJournal journal)
    {
        if (journal == null) return false;

        if (RequireCompletedQuests != null)
        {
            for (int i = 0; i < RequireCompletedQuests.Count; i++)
            {
                var id = RequireCompletedQuests[i];
                if (!string.IsNullOrWhiteSpace(id) && !journal.IsCompleted(id))
                    return false;
            }
        }

        if (RequireActiveQuests != null)
        {
            for (int i = 0; i < RequireActiveQuests.Count; i++)
            {
                var id = RequireActiveQuests[i];
                if (!string.IsNullOrWhiteSpace(id) && !journal.IsActive(id))
                    return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(RequireFlagId) && WorldFlags.Get(RequireFlagId) != RequireFlagValue)
            return false;

        var tm = TimeManager.Instance;
        if (tm != null)
        {
            if (RestrictByDay && (AllowedDays & DayOfWeekMaskExtensions.From(tm.DayOfWeek)) == 0)
                return false;

            if (RestrictByPhase && (AllowedPhases & DayPhaseMaskExtensions.From(tm.TimeOfDay)) == 0)
                return false;
        }

        return true;
    }
}

[Serializable]
public class QuestStepDefinition
{
    [Header("Meta")]
    public string StepId;
    [TextArea(1, 4)] public string Text;
    public QuestStepType Type = QuestStepType.Manual;

    [Header("Location (ReachLocation)")]
    public string TargetLocationSceneName;

    [Header("Time Restrictions (optional; gates ANY step)")]
    public bool RestrictByDay = false;
    public DayOfWeekMask AllowedDays = DayOfWeekMask.All;

    public bool RestrictByPhase = false;
    public DayPhaseMask AllowedPhases = DayPhaseMask.All;

    [Header("Stats requirements (MinStats)")]
    public int RequiredInfluence = 0;
    public int RequiredStrategy = 0;
    public int RequiredNetworking = 0;
    public int RequiredReputation = 0;

    [Header("Money (HaveMoney / PayMoney)")]
    public int RequiredMoney = 0;

    [Header("Dialogue (TalkToDialogue)")]
    public string TargetNpcId;
    public string TargetDialogueId;

    [Header("Inventory (HaveItem)")]
    public ItemAmount RequiredItem;

    [Header("World Flag (FlagTrue)")]
    [FlagId] public string RequiredFlagId;
}