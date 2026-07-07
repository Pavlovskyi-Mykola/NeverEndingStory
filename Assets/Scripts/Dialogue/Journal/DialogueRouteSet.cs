using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueRouteSet", menuName = "Game/Dialogue/Dialogue Route Set")]
public class DialogueRouteSet : ScriptableObject
{
    [Tooltip("Eligible rules play in tier order (Quest > Event > Routine > SmallTalk), then by Priority, then by list order. Rules default to Auto tier: quest-gated rules jump to the Quest tier, everything else is Routine and keeps the old top-to-bottom behaviour. Use the Route Set Editor window for a readable view of the flow.")]
    public List<DialogueSelectorRule> rules = new();

    [Tooltip("Fallback if no rules match.")]
    public DialogueGraph fallback;
}
