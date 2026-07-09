using System;
using UnityEngine;

public enum DialogueRuleOutput
{
    // int 1 preserves data for rules that were serialized as SpecialIfCondition
    SingleGraph = 1,
    Pool        = 2,
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

/// <summary>
/// Ordering tier for eligible rules: lower value plays first.
/// Auto (the default for all pre-existing rules) resolves to Quest when the
/// rule is gated on an Active quest, otherwise Routine.
/// </summary>
public enum DialogueRuleTier
{
    Auto = 0,
    Quest = 1,     // quest-critical dialogue — always outranks chatter
    Event = 2,     // calendar-event / special-day dialogue
    Routine = 3,   // daily routine talk
    SmallTalk = 4  // filler chatter, lowest priority
}

/// <summary>
/// One routing rule: "under this coarse world state, play this conversation."
/// Scope is deliberately limited to SELECTION between whole conversations
/// (quest state, relationship tier, seen/unseen, time/place). Fine-grained,
/// in-conversation logic (stat checks, flag branches) belongs in the graph's
/// Branch nodes + DialogueConditions, not here — that duplication was removed.
/// </summary>
[Serializable]
public class DialogueSelectorRule
{
    [Header("Output")]
    [Tooltip("SingleGraph: return one graph when conditions pass. Pool: pick from the pool[] array.")]
    public DialogueRuleOutput output = DialogueRuleOutput.SingleGraph;

    [Header("Priority")]
    [Tooltip("Eligible rules play in tier order: Quest > Event > Routine > SmallTalk. Auto = Quest when this rule is gated on an Active quest (or a quest step), otherwise Routine. Ties resolve by Priority, then list order.")]
    public DialogueRuleTier tier = DialogueRuleTier.Auto;

    [Tooltip("Tie-breaker within the same tier: higher plays first. Equal priorities keep list order.")]
    public int priority = 0;

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
    [QuestId] public string requiredQuestId;
    public QuestRouteState requiredQuestState = QuestRouteState.Any;

    [Tooltip("Optional. Only checked when Required Quest Id is set and the quest is active.")]
    public string requiredQuestStepId;

    [Tooltip("Optional. Use -1 to ignore.")]
    public int requiredQuestStepIndex = -1;

    [Header("Relationship (optional)")]
    [Tooltip("Minimum relationship level with this NPC to select the rule. 0 = ignored.")]
    public int requiredRelationshipLevel = 0;

    /// <summary>
    /// Tier used for ordering. Auto infers Quest only for rules gated on an
    /// Active quest (or a specific step) — NotStarted offers and Completed
    /// epilogues stay Routine unless explicitly tagged, since those gates can
    /// hold true forever and would permanently mask other dialogue.
    /// </summary>
    public DialogueRuleTier GetEffectiveTier()
    {
        if (tier != DialogueRuleTier.Auto)
            return tier;

        bool activeQuestGated =
            !string.IsNullOrWhiteSpace(requiredQuestId) &&
            (requiredQuestState == QuestRouteState.Active ||
             !string.IsNullOrWhiteSpace(requiredQuestStepId) ||
             requiredQuestStepIndex >= 0);

        return activeQuestGated ? DialogueRuleTier.Quest : DialogueRuleTier.Routine;
    }

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

        if (!PassesRelationship(ctx))
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

    private bool PassesRelationship(DialogueSelectorContext ctx)
    {
        if (requiredRelationshipLevel <= 0)
            return true;

        return RelationshipManager.Instance != null &&
               !string.IsNullOrEmpty(ctx.NpcId) &&
               RelationshipManager.Instance.GetLevel(ctx.NpcId) >= requiredRelationshipLevel;
    }
}
