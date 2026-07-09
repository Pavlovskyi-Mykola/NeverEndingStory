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

    // Tier ascending (Quest first), priority descending, authored order last.
    // Shared by dialogue selection and placement resolution so "where the NPC
    // stands" and "what they say" always agree on which rule wins.
    private static readonly System.Comparison<Candidate> CandidateOrder = static (a, b) =>
    {
        int byTier = a.Tier.CompareTo(b.Tier);
        if (byTier != 0) return byTier;

        int byPriority = b.Rule.priority.CompareTo(a.Rule.priority);
        if (byPriority != 0) return byPriority;

        return a.Index.CompareTo(b.Index);
    };

    /// <summary>
    /// Resolves where the NPC should be right now from its dialogue rules: the
    /// highest-ranked eligible rule with a placement wins. Returns false when the
    /// NPC should not be spawned anywhere (no placement rule matches, or the
    /// winning rule is Hidden / has no valid location).
    /// </summary>
    public static bool TryResolvePlacement(NpcDefinition npc, DialogueSelectorContext ctx,
        out SceneReference location, out string spawnPointKey)
    {
        location = null;
        spawnPointKey = null;

        if (npc == null || npc.DialogueRules == null || npc.DialogueRules.Count == 0)
            return false;

        var candidates = new List<Candidate>(npc.DialogueRules.Count);

        for (int i = 0; i < npc.DialogueRules.Count; i++)
        {
            var rule = npc.DialogueRules[i];
            if (rule == null || !rule.IsEligibleForPlacement(ctx))
                continue;

            candidates.Add(new Candidate { Rule = rule, Tier = rule.GetEffectiveTier(), Index = i });
        }

        if (candidates.Count == 0)
            return false;

        candidates.Sort(CandidateOrder);

        var winner = candidates[0].Rule;

        if (winner.placement != NpcPlacement.AtLocation)
            return false; // Hidden — explicitly absent

        if (winner.locationScene == null || !winner.locationScene.IsValid)
            return false;

        location = winner.locationScene;
        spawnPointKey = winner.spawnPointKey;
        return true;
    }

    /// <summary>Primary entry: routing lives inline on NpcDefinition.</summary>
    public static DialogueGraph Select(NpcDefinition npc, DialogueSelectorContext ctx)
    {
        if (npc == null)
            return null;

        return Select(npc.DialogueRules, npc.DialogueFallback, ctx);
    }

    public static DialogueGraph Select(IReadOnlyList<DialogueSelectorRule> rules, DialogueGraph fallback, DialogueSelectorContext ctx)
    {
        if (rules != null && rules.Count > 0)
        {
            // Eligibility first, then order by tier (Quest > Event > Routine >
            // SmallTalk), priority within a tier, and finally authored list
            // order — so an NPC's quest dialogue wins over their routine talk
            // and small talk no matter where it sits in the list.
            var candidates = new List<Candidate>(rules.Count);

            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || !rule.IsEligible(ctx))
                    continue;

                candidates.Add(new Candidate { Rule = rule, Tier = rule.GetEffectiveTier(), Index = i });
            }

            candidates.Sort(CandidateOrder);

            for (int i = 0; i < candidates.Count; i++)
            {
                // A rule can still come up empty (e.g. an exhausted one-time pool);
                // fall through to the next candidate like the old top-to-bottom pass.
                var result = ResolveRule(candidates[i].Rule, ctx);
                if (result != null)
                    return result;
            }
        }

        return fallback;
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
