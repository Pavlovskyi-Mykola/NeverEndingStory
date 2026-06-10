using System;
using UnityEngine;

public enum DialogueRuleKind
{
    IntroIfNotSeen,
    SpecialIfCondition,
    RepeatablePool
}

public enum DialoguePickMode
{
    FirstValid,
    Random
}

public enum QuestRouteState
{
    Any = 0,
    NotStarted = 1,
    Active = 2,
    Completed = 3
}

[Serializable]
public class DialogueSelectorRule
{
    [Header("Rule Type")]
    public DialogueRuleKind kind = DialogueRuleKind.SpecialIfCondition;

    [Header("Graph / Pool")]
    public DialogueGraph graph;
    public DialogueGraph[] pool;
    public DialoguePickMode pickMode = DialoguePickMode.Random;

    [Header("World Gating")]
    public DayPhaseMask allowedPhases = DayPhaseMask.All;
    public DayOfWeekMask allowedDays = DayOfWeekMask.All;
    public string[] allowedLocationIds;

    [Header("Seen / Unseen Gating")]
    public bool requireNotSeenDialogue;
    public DialogueGraph requireNotSeenThis;
    public DialogueGraph requireSeenThis;

    [Header("Quest Gating")]
    public string requiredQuestId;
    public QuestRouteState requiredQuestState = QuestRouteState.Any;

    [Tooltip("Optional. Only checked when Required Quest Id is set and the quest is active.")]
    public string requiredQuestStepId;

    [Tooltip("Optional. Use -1 to ignore.")]
    public int requiredQuestStepIndex = -1;

    [Header("Flags (optional)")]
    public string requiredFlagId;
    public bool requiredFlagValue = true;

    [Header("Stats (optional)")]
    public int requiredMoney = 0;
    public int requiredInfluence = 0;
    public int requiredStrategy = 0;
    public int requiredNetworking = 0;
    public int requiredReputation = 0;

    [Header("Optional graph-level conditions")]
    public DialogueConditionGroup extraConditions;

    public bool IsEligible(DialogueSelectorContext ctx)
    {
        var journal = DialogueJournal.Instance;

        if (!PassesPhase(ctx))
            return false;

        if (!PassesDay(ctx))
            return false;

        if (!PassesLocation(ctx))
            return false;

        if (!PassesSeenChecks(journal))
            return false;

        if (!PassesQuestChecks(ctx))
            return false;

        if (!PassesFlagChecks(ctx))
            return false;

        if (!PassesStatsChecks(ctx))
            return false;

        if (extraConditions != null && !extraConditions.Evaluate())
            return false;

        return true;
    }

    public bool IsPoolGraphEligible(DialogueGraph candidate, DialogueSelectorContext ctx)
    {
        if (candidate == null)
            return false;

        var journal = DialogueJournal.Instance;

        if (requireNotSeenDialogue && journal != null && journal.HasSeenDialogue(candidate.DialogueId))
            return false;

        return true;
    }

    private bool PassesPhase(DialogueSelectorContext ctx)
    {
        return (allowedPhases & DayPhaseMaskExtensions.From(ctx.Phase)) != 0;
    }

    private bool PassesDay(DialogueSelectorContext ctx)
    {
        return (allowedDays & DayOfWeekMaskExtensions.From(ctx.Day)) != 0;
    }

    private bool PassesLocation(DialogueSelectorContext ctx)
    {
        if (allowedLocationIds == null || allowedLocationIds.Length == 0)
            return true;

        for (int i = 0; i < allowedLocationIds.Length; i++)
        {
            if (string.Equals(allowedLocationIds[i], ctx.LocationId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool PassesSeenChecks(DialogueJournal journal)
    {
        if (requireNotSeenDialogue && graph != null && journal != null && journal.HasSeenDialogue(graph.DialogueId))
            return false;

        if (requireNotSeenThis != null && journal != null && journal.HasSeenDialogue(requireNotSeenThis.DialogueId))
            return false;

        if (requireSeenThis != null)
        {
            if (journal == null)
                return false;

            if (!journal.HasSeenDialogue(requireSeenThis.DialogueId))
                return false;
        }

        return true;
    }

    private bool PassesQuestChecks(DialogueSelectorContext ctx)
    {
        if (string.IsNullOrWhiteSpace(requiredQuestId))
            return true;

        var actualState = ctx.GetQuestState(requiredQuestId);

        if (requiredQuestState != QuestRouteState.Any && actualState != requiredQuestState)
            return false;

        if (!string.IsNullOrWhiteSpace(requiredQuestStepId))
        {
            if (actualState != QuestRouteState.Active)
                return false;

            if (!ctx.IsQuestOnStepId(requiredQuestId, requiredQuestStepId))
                return false;
        }

        if (requiredQuestStepIndex >= 0)
        {
            if (actualState != QuestRouteState.Active)
                return false;

            if (!ctx.IsQuestOnStepIndex(requiredQuestId, requiredQuestStepIndex))
                return false;
        }

        return true;
    }

    private bool PassesFlagChecks(DialogueSelectorContext ctx)
    {
        if (string.IsNullOrWhiteSpace(requiredFlagId))
            return true;

        return ctx.CheckFlag(requiredFlagId, requiredFlagValue);
    }

    private bool PassesStatsChecks(DialogueSelectorContext ctx)
    {
        if (ctx.Money < requiredMoney)
            return false;

        if (ctx.Influence < requiredInfluence)
            return false;

        if (ctx.Strategy < requiredStrategy)
            return false;

        if (ctx.Networking < requiredNetworking)
            return false;

        if (ctx.Reputation < requiredReputation)
            return false;

        return true;
    }
}