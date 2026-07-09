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
/// How a rule affects the NPC's physical presence in the world. Placement and
/// dialogue share one rule so the conversation can never be authored for a
/// time/place the NPC isn't actually standing in.
/// </summary>
public enum NpcPlacement
{
    /// <summary>Dialogue-only rule: plays wherever the NPC happens to be. Never drives spawning.</summary>
    WhereverNpcIs = 0,
    /// <summary>Puts the NPC at LocationScene/SpawnPointKey during the rule's days+phases; the dialogue only plays there.</summary>
    AtLocation = 1,
    /// <summary>Forces the NPC absent during the rule's days+phases (e.g. gone during a quest). Plays no dialogue.</summary>
    Hidden = 2
}

/// <summary>
/// One routing rule: "under this coarse world state, play this conversation."
/// Scope is deliberately limited to SELECTION between whole conversations
/// (quest state, relationship tier, seen/unseen, time/place). Fine-grained,
/// in-conversation logic (stat checks, flag branches) belongs in the graph's
/// Branch nodes + DialogueConditions, not here — that duplication was removed.
/// A rule with Placement = AtLocation also drives WHERE the NPC spawns, so
/// presence and dialogue are always authored together.
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

    [Header("Where & When")]
    [Tooltip("AtLocation: the NPC stands at Location Scene / Spawn Point during the days+phases below, and this dialogue only plays there. WhereverNpcIs: dialogue-only, plays at any location. Hidden: the NPC is absent during the window.")]
    public NpcPlacement placement = NpcPlacement.WhereverNpcIs;
    public SceneReference locationScene;
    [SpawnPointKey] public string spawnPointKey;
    public DayPhaseMask allowedPhases = DayPhaseMask.All;
    public DayOfWeekMask allowedDays = DayOfWeekMask.All;

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
        // Hidden rules only remove the NPC from the world — they never speak.
        if (placement == NpcPlacement.Hidden)
            return false;

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

    /// <summary>
    /// Eligibility for driving the NPC's presence (spawn/despawn). Same gates as
    /// IsEligible except location — the rule's own placement decides the location.
    /// WhereverNpcIs rules never drive presence.
    /// </summary>
    public bool IsEligibleForPlacement(DialogueSelectorContext ctx)
    {
        if (placement == NpcPlacement.WhereverNpcIs)
            return false;

        var journal = DialogueJournal.Instance;

        return PassesPhase(ctx)
            && PassesDay(ctx)
            && PassesSeenChecks(journal)
            && PassesQuestChecks(ctx)
            && PassesRelationship(ctx);
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
        // Only AtLocation rules are location-bound: the dialogue plays exactly
        // where the rule places the NPC, never anywhere else.
        if (placement != NpcPlacement.AtLocation)
            return true;

        if (locationScene == null || !locationScene.IsValid)
            return false;

        return string.Equals(locationScene.SceneName, ctx.LocationId, StringComparison.OrdinalIgnoreCase);
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
