using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueRouteSet", menuName = "Game/Dialogue/Dialogue Route Set")]
public class DialogueRouteSet : ScriptableObject
{
    [Tooltip("Rules are evaluated top-to-bottom. First match wins (except RepeatablePool which picks inside the pool).")]
    public List<DialogueSelectorRule> rules = new();

    [Tooltip("Fallback if no rules match.")]
    public DialogueGraph fallback;
}
