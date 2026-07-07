using System.Collections.Generic;
using UnityEngine;

public static class DialogueSelector
{
    private struct Candidate
    {
        public DialogueSelectorRule Rule;
        public DialogueRuleTier Tier;
        public int Index;
    }

    public static DialogueGraph Select(DialogueRouteSet routes, DialogueSelectorContext ctx)
    {
        if (routes == null)
            return null;

        if (routes.rules != null && routes.rules.Count > 0)
        {
            // Eligibility first, then order by tier (Quest > Event > Routine >
            // SmallTalk), priority within a tier, and finally authored list
            // order — so an NPC's quest dialogue wins over their routine talk
            // and small talk no matter where it sits in the list.
            var candidates = new List<Candidate>(routes.rules.Count);

            for (int i = 0; i < routes.rules.Count; i++)
            {
                var rule = routes.rules[i];
                if (rule == null || !rule.IsEligible(ctx))
                    continue;

                candidates.Add(new Candidate { Rule = rule, Tier = rule.GetEffectiveTier(), Index = i });
            }

            candidates.Sort(static (a, b) =>
            {
                int byTier = a.Tier.CompareTo(b.Tier);
                if (byTier != 0) return byTier;

                int byPriority = b.Rule.priority.CompareTo(a.Rule.priority);
                if (byPriority != 0) return byPriority;

                return a.Index.CompareTo(b.Index);
            });

            for (int i = 0; i < candidates.Count; i++)
            {
                // A rule can still come up empty (e.g. an exhausted one-time pool);
                // fall through to the next candidate like the old top-to-bottom pass.
                var result = ResolveRule(candidates[i].Rule, ctx);
                if (result != null)
                    return result;
            }
        }

        return routes.fallback;
    }

    private static DialogueGraph ResolveRule(DialogueSelectorRule rule, DialogueSelectorContext ctx)
    {
        switch (rule.output)
        {
            case DialogueRuleOutput.Pool:
                return PickFromPool(rule, ctx);

            default: // SingleGraph, and legacy int-0 IntroIfNotSeen assets
                return rule.graph;
        }
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