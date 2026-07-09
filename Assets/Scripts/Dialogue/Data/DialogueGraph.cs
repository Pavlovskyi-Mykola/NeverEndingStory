using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueGraph", menuName = "Game/Dialogue/Dialogue Graph")]
public class DialogueGraph : ScriptableObject
{
    [SerializeField, HideInInspector]
    private string dialogueId; // Stable GUID-like ID (never changes)

    [SerializeField]
    private string startNodeId;

    [Tooltip("When on, finishing this conversation counts as relationship-building: the NPC's first-talk-of-day bonus applies. Leave off for small talk / functional dialogues.")]
    [SerializeField]
    private bool countsAsRelationshipTalk = false;

    // SerializeReference lets Unity store derived node types in a single list.
    [SerializeReference]
    private List<DialogueNode> nodes = new();

    public string DialogueId => dialogueId;

    /// <summary>
    /// Id used for journal marking and seen-gates. Falls back to the asset name
    /// when the GUID hasn't been generated yet, so "mark seen" (runner) and
    /// "has seen" (selector rules) can never disagree about the key.
    /// </summary>
    public string EffectiveDialogueId => string.IsNullOrEmpty(dialogueId) ? name : dialogueId;
    public string StartNodeId => startNodeId;
    public bool CountsAsRelationshipTalk => countsAsRelationshipTalk;
    public IReadOnlyList<DialogueNode> Nodes => nodes;

    private Dictionary<string, DialogueNode> _nodesById;

    public DialogueNode GetNode(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (_nodesById == null)
            RebuildLookup();

        return _nodesById.TryGetValue(id, out var node) ? node : null;
    }

    public bool HasNode(string id) => GetNode(id) != null;

    private void RebuildLookup()
    {
        _nodesById = new Dictionary<string, DialogueNode>(StringComparer.Ordinal);

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n != null && !string.IsNullOrEmpty(n.Id))
                _nodesById[n.Id] = n;
        }
    }

    private void OnEnable()
    {
        _nodesById = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Graph editor edits go through SerializedObject, which triggers OnValidate —
        // drop the lookup so it rebuilds against the new node list.
        _nodesById = null;

        // Generate GUID once if missing
        if (string.IsNullOrEmpty(dialogueId))
        {
            dialogueId = Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}

