using System.Collections.Generic;
using UnityEngine;

public static class DialogueSelector
{
    public static DialogueGraph Select(DialogueRouteSet routes, DialogueSelectorContext ctx)
    {
        if (routes == null)
            return null;

        if (routes.rules != null)
        {
            for (int i = 0; i < routes.rules.Count; i++)
            {
                var rule = routes.rules[i];
                if (rule == null)
                    continue;

                if (!rule.IsEligible(ctx))
                    continue;

                var result = ResolveRule(rule, ctx);
                if (result != null)
                    return result;
            }
        }

        return routes.fallback;
    }

    private static DialogueGraph ResolveRule(DialogueSelectorRule rule, DialogueSelectorContext ctx)
    {
        switch (rule.kind)
        {
            case DialogueRuleKind.IntroIfNotSeen:
                return ResolveIntro(rule);

            case DialogueRuleKind.SpecialIfCondition:
                return rule.graph;

            case DialogueRuleKind.RepeatablePool:
                return PickFromPool(rule, ctx);

            default:
                return null;
        }
    }

    private static DialogueGraph ResolveIntro(DialogueSelectorRule rule)
    {
        if (rule.graph == null)
            return null;

        var journal = DialogueJournal.Instance;
        if (journal != null && journal.HasSeenDialogue(rule.graph.DialogueId))
            return null;

        return rule.graph;
    }

    private static DialogueGraph PickFromPool(DialogueSelectorRule rule, DialogueSelectorContext ctx)
    {
        if (rule.pool == null || rule.pool.Length == 0)
            return null;

        var candidates = new List<DialogueGraph>(rule.pool.Length);

        for (int i = 0; i < rule.pool.Length; i++)
        {
            var graph = rule.pool[i];
            if (graph == null)
                continue;

            if (!rule.IsPoolGraphEligible(graph, ctx))
                continue;

            candidates.Add(graph);
        }

        if (candidates.Count == 0)
            return null;

        if (rule.pickMode == DialoguePickMode.FirstValid)
            return candidates[0];

        return candidates[Random.Range(0, candidates.Count)];
    }
}