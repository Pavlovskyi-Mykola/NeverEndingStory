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

    // SerializeReference lets Unity store derived node types in a single list.
    [SerializeReference]
    private List<DialogueNode> nodes = new();

    public string DialogueId => dialogueId;
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

    public bool HasNode(string id) => GetNode(id) != null;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Generate GUID once if missing
        if (string.IsNullOrEmpty(dialogueId))
        {
            dialogueId = Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}

