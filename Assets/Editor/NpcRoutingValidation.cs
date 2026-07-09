#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

/// <summary>
/// Shared validation for an NPC's dialogue routing + placement, used by both
/// NpcDefinitionEditor and NpcRoutingWindow. Since placement (where the NPC
/// stands) merged into the dialogue rules, the checks focus on the mistakes
/// that merge was meant to catch: dead rules, missing locations, time windows
/// where the NPC is nowhere, and ambiguous overlapping placements.
/// </summary>
public static class NpcRoutingValidation
{
    public struct Message
    {
        public MessageType Type;
        public string Text;

        public Message(MessageType type, string text) { Type = type; Text = text; }
    }

    private static readonly string[] DayNames   = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly string[] PhaseNames = { "Morning", "Afternoon", "Evening", "Night" };

    public static List<Message> Validate(NpcDefinition npc)
    {
        var messages = new List<Message>();

        if (npc == null)
            return messages;

        if (npc.Schedule != null && npc.Schedule.Count > 0)
            messages.Add(new Message(MessageType.Warning,
                $"{npc.Schedule.Count} legacy schedule entr(ies) are no longer used at runtime — convert them to placement rules (button below)."));

        var rules = npc.DialogueRules;

        if (rules == null || rules.Count == 0)
        {
            if (npc.DialogueFallback == null)
                messages.Add(new Message(MessageType.Warning, "No rules and no fallback — this NPC has nothing to say."));

            messages.Add(new Message(MessageType.Warning, "No placement rules — this NPC never appears anywhere."));
            return messages;
        }

        int deadRules = 0, missingLocations = 0, hiddenWithGraph = 0, emptyMasks = 0;

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule == null) { deadRules++; continue; }

            // Unity zeroes fields on list-added elements (initializers don't run),
            // so a fresh rule can start with no days/phases — never eligible.
            if (rule.allowedDays == 0 || rule.allowedPhases == 0)
                emptyMasks++;

            bool hasOutput = rule.output == DialogueRuleOutput.Pool
                ? rule.pool != null && rule.pool.Any(g => g != null)
                : rule.graph != null;

            switch (rule.placement)
            {
                case NpcPlacement.WhereverNpcIs:
                    // Pure dialogue rule — pointless without something to play.
                    if (!hasOutput) deadRules++;
                    break;

                case NpcPlacement.AtLocation:
                    // Presence-only (no graph) is fine; a missing location is not.
                    if (rule.locationScene == null || !rule.locationScene.IsValid) missingLocations++;
                    break;

                case NpcPlacement.Hidden:
                    if (hasOutput) hiddenWithGraph++;
                    break;
            }
        }

        if (emptyMasks > 0)
            messages.Add(new Message(MessageType.Warning,
                $"{emptyMasks} rule(s) have no Days or no Phases selected — they can never match. (New rules start empty; toggle the buttons.)"));

        if (deadRules > 0)
            messages.Add(new Message(MessageType.Warning,
                $"{deadRules} rule(s) have no graph/pool assigned — they can never play."));

        if (missingLocations > 0)
            messages.Add(new Message(MessageType.Warning,
                $"{missingLocations} AtLocation rule(s) have no Location Scene — the NPC can't be placed by them."));

        if (hiddenWithGraph > 0)
            messages.Add(new Message(MessageType.Info,
                $"{hiddenWithGraph} Hidden rule(s) have a graph/pool assigned — Hidden rules never play dialogue."));

        ValidateCoverage(rules, messages);
        ValidateAmbiguousPlacements(rules, messages);

        if (npc.DialogueFallback == null)
            messages.Add(new Message(MessageType.Info, "No Dialogue Fallback — if no rule matches, the NPC won't talk."));

        return messages;
    }

    /// <summary>
    /// Day×phase cells not covered by any placement rule: the NPC exists nowhere
    /// during those windows. Intentional absence should use Hidden so it reads as
    /// a decision, not an oversight.
    /// </summary>
    private static void ValidateCoverage(List<DialogueSelectorRule> rules, List<Message> messages)
    {
        bool anyPlacement = rules.Any(r => r != null && r.placement != NpcPlacement.WhereverNpcIs);
        if (!anyPlacement)
        {
            messages.Add(new Message(MessageType.Warning,
                "No placement rules (AtLocation/Hidden) — this NPC never appears anywhere."));
            return;
        }

        var uncovered = new List<string>();

        for (int d = 0; d < 7; d++)
        {
            for (int p = 0; p < 4; p++)
            {
                int dayBit = 1 << d, phaseBit = 1 << p;

                bool covered = rules.Any(r => r != null
                    && r.placement != NpcPlacement.WhereverNpcIs
                    && ((int)r.allowedDays & dayBit) != 0
                    && ((int)r.allowedPhases & phaseBit) != 0);

                if (!covered)
                    uncovered.Add($"{DayNames[d]} {PhaseNames[p]}");
            }
        }

        if (uncovered.Count == 0)
            return;

        const int maxListed = 6;
        string list = string.Join(", ", uncovered.Take(maxListed));
        if (uncovered.Count > maxListed)
            list += $" (+{uncovered.Count - maxListed} more)";

        messages.Add(new Message(MessageType.Info,
            $"NPC has no placement (appears nowhere) during: {list}. Use a Hidden rule if that's intentional."));
    }

    /// <summary>
    /// Two unconditional placement rules that overlap in time, tie on tier and
    /// priority, but place the NPC differently — list order silently decides,
    /// which is exactly the kind of mismatch the merge is meant to surface.
    /// </summary>
    private static void ValidateAmbiguousPlacements(List<DialogueSelectorRule> rules, List<Message> messages)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            var a = rules[i];
            if (!IsUnconditionalPlacement(a)) continue;

            for (int j = i + 1; j < rules.Count; j++)
            {
                var b = rules[j];
                if (!IsUnconditionalPlacement(b)) continue;

                if ((a.allowedDays & b.allowedDays) == 0 || (a.allowedPhases & b.allowedPhases) == 0)
                    continue;

                if (a.GetEffectiveTier() != b.GetEffectiveTier() || a.priority != b.priority)
                    continue;

                if (SamePlace(a, b))
                    continue;

                messages.Add(new Message(MessageType.Warning,
                    $"Rules #{i} and #{j} both place the NPC during the same time window with equal tier/priority but different locations — #{i} silently wins by list order. Give one a higher priority or disjoint days/phases."));
                return; // one example is enough; more would just be noise
            }
        }
    }

    private static bool IsUnconditionalPlacement(DialogueSelectorRule r)
    {
        return r != null
            && r.placement != NpcPlacement.WhereverNpcIs
            && string.IsNullOrWhiteSpace(r.requiredQuestId)
            && r.requiredRelationshipLevel <= 0
            && !r.requireNotSeenDialogue
            && r.requireNotSeenThis == null
            && r.requireSeenThis == null;
    }

    private static bool SamePlace(DialogueSelectorRule a, DialogueSelectorRule b)
    {
        if (a.placement != b.placement) return false;
        if (a.placement == NpcPlacement.Hidden) return true;

        string sa = a.locationScene != null ? a.locationScene.SceneName : "";
        string sb = b.locationScene != null ? b.locationScene.SceneName : "";
        return sa == sb && (a.spawnPointKey ?? "") == (b.spawnPointKey ?? "");
    }
}

/// <summary>
/// Rule-list operations shared by NpcDefinitionEditor and NpcRoutingWindow.
/// </summary>
public static class NpcRoutingEditorUtility
{
    /// <summary>
    /// Stable sort: tier ascending, then priority descending — matches
    /// DialogueSelector's evaluation order (ties keep authored order).
    /// </summary>
    public static void SortRulesToRuntimeOrder(NpcDefinition npc)
    {
        UnityEditor.Undo.RecordObject(npc, "Sort Dialogue Rules");

        var sorted = npc.DialogueRules
            .Select((rule, index) => (rule, index))
            .OrderBy(x => x.rule != null ? (int)x.rule.GetEffectiveTier() : int.MaxValue)
            .ThenByDescending(x => x.rule != null ? x.rule.priority : int.MinValue)
            .ThenBy(x => x.index)
            .Select(x => x.rule)
            .ToList();

        npc.DialogueRules.Clear();
        npc.DialogueRules.AddRange(sorted);

        EditorUtility.SetDirty(npc);
    }

    /// <summary>
    /// Search-filter match over a rule: graph/pool names, quest id, placement
    /// location/spawn point, and tier name.
    /// </summary>
    public static bool RuleMatches(NpcDefinition npc, int index, string search)
    {
        if (npc.DialogueRules == null || index >= npc.DialogueRules.Count)
            return false;

        var rule = npc.DialogueRules[index];
        if (rule == null)
            return false;

        if (rule.graph != null && rule.graph.name.ToLowerInvariant().Contains(search))
            return true;

        if (rule.pool != null)
        {
            for (int i = 0; i < rule.pool.Length; i++)
            {
                if (rule.pool[i] != null && rule.pool[i].name.ToLowerInvariant().Contains(search))
                    return true;
            }
        }

        if (!string.IsNullOrEmpty(rule.requiredQuestId) &&
            rule.requiredQuestId.ToLowerInvariant().Contains(search))
            return true;

        if (rule.placement == NpcPlacement.AtLocation)
        {
            if (rule.locationScene != null && rule.locationScene.IsValid &&
                rule.locationScene.SceneName.ToLowerInvariant().Contains(search))
                return true;

            if (!string.IsNullOrEmpty(rule.spawnPointKey) &&
                rule.spawnPointKey.ToLowerInvariant().Contains(search))
                return true;
        }

        return rule.GetEffectiveTier().ToString().ToLowerInvariant().Contains(search);
    }
}
#endif
