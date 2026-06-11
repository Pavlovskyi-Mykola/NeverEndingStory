using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueRouteSet", menuName = "Game/Dialogue/Dialogue Route Set")]
public class DialogueRouteSet : ScriptableObject
{
    [Tooltip("Rules are evaluated top-to-bottom. First rule whose conditions pass wins. Use the Route Set Editor window for a readable view of the flow.")]
    public List<DialogueSelectorRule> rules = new();

    [Tooltip("Fallback if no rules match.")]
    public DialogueGraph fallback;
}
