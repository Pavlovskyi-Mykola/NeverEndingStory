using System;
using UnityEngine;

public enum DialogueRuleKind
{
    IntroIfNotSeen,      // play once
    SpecialIfCondition,  // play when condition is true
    RepeatablePool       // choose from pool (optionally random)
}

public enum DialoguePickMode
{
    FirstValid,
    Random
}

[Serializable]
public class DialogueSelectorRule
{
    public DialogueRuleKind kind;

    [Header("Graph / Pool")]
    public DialogueGraph graph;              // used by Intro/Special
    public DialogueGraph[] pool;             // used by RepeatablePool
    public DialoguePickMode pickMode = DialoguePickMode.Random;

    [Header("Optional conditions (can combine)")]
    public DayPhaseMask allowedPhases = DayPhaseMask.All;
    public bool requireNotSeenDialogue;      // extra guard
    public DialogueGraph requireNotSeenThis; // if set -> require not seen

    // Later extensions (placeholders):
    // public string requiredQuestFlag;
    // public int requiredStrength;
    // public int requiredMoney;

    public bool IsEligible(DialogueSelectorContext ctx, DialogueJournal journal)
    {
        // Phase constraint
        if (!allowedPhases.HasFlag(DayPhaseMaskExtensions.From(ctx.Phase)))
            return false;

        // Not-seen constraints
        if (requireNotSeenDialogue && graph != null && journal != null && journal.HasSeenDialogue(graph.DialogueId))
            return false;

        if (requireNotSeenThis != null && journal != null && journal.HasSeenDialogue(requireNotSeenThis.DialogueId))
            return false;

        // If you add quests/stats later, check them here.

        return true;
    }
}
