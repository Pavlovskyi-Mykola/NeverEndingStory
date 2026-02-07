using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueGraph", menuName = "Game/Dialogue/Dialogue Graph")]
public class DialogueGraph : ScriptableObject
{
    [SerializeField] private string startNodeId;

    // SerializeReference lets Unity store derived node types in a single list.
    [SerializeReference] private List<DialogueNode> nodes = new();

    public string StartNodeId => startNodeId;
    public IReadOnlyList<DialogueNode> Nodes => nodes;

    public DialogueNode GetNode(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null && nodes[i].Id == id)
                return nodes[i];
        return null;
    }

    // Helpful for debugging.
    public bool HasNode(string id) => GetNode(id) != null;
}
