using System.Collections.Generic;
using UnityEngine;

public static class DialogueSelector
{
    public static DialogueGraph Select(DialogueRouteSet routes, DialogueSelectorContext ctx)
    {
        if (routes == null) return null;

        var journal = DialogueJournal.Instance;

        for (int i = 0; i < routes.rules.Count; i++)
        {
            var rule = routes.rules[i];
            if (rule == null) continue;
            if (!rule.IsEligible(ctx, journal)) continue;

            switch (rule.kind)
            {
                case DialogueRuleKind.IntroIfNotSeen:
                    {
                        if (rule.graph == null) break;
                        if (journal != null && journal.HasSeenDialogue(rule.graph.DialogueId)) break;
                        return rule.graph;
                    }

                case DialogueRuleKind.SpecialIfCondition:
                    {
                        if (rule.graph == null) break;
                        return rule.graph;
                    }

                case DialogueRuleKind.RepeatablePool:
                    {
                        var chosen = PickFromPool(rule, journal);
                        if (chosen != null) return chosen;
                        break;
                    }
            }
        }

        return routes.fallback;
    }

    private static DialogueGraph PickFromPool(DialogueSelectorRule rule, DialogueJournal journal)
    {
        if (rule.pool == null || rule.pool.Length == 0) return null;

        // Build list of eligible graphs (optionally excluding seen, etc.)
        var candidates = new List<DialogueGraph>(rule.pool.Length);
        for (int i = 0; i < rule.pool.Length; i++)
        {
            var g = rule.pool[i];
            if (g == null) continue;

            // Optional: if "requireNotSeenDialogue" used with pools, treat it as "exclude seen"
            if (rule.requireNotSeenDialogue && journal != null && journal.HasSeenDialogue(g.DialogueId))
                continue;

            candidates.Add(g);
        }

        if (candidates.Count == 0) return null;

        if (rule.pickMode == DialoguePickMode.FirstValid)
            return candidates[0];

        // Random
        return candidates[Random.Range(0, candidates.Count)];
    }
}
