using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional per-dialogue progress container.
/// Unity-serializable (so it can be saved in Snapshot).
/// </summary>
[Serializable]
public class DialogueProgress
{
    [SerializeField] private string dialogueId;

    // Simple useful counters
    [SerializeField] private int timesStarted;
    [SerializeField] private int timesFinished;

    // "Where did we end last time"
    [SerializeField] private string lastEndNodeId;

    // ISO timestamp string (keeps it Unity-serializable & simple)
    [SerializeField] private string lastFinishedUtc;

    // Optional granular progress
    [SerializeField] private List<string> visitedNodeIds = new();

    public string DialogueId => dialogueId;

    public int TimesStarted
    {
        get => timesStarted;
        set => timesStarted = value;
    }

    public int TimesFinished
    {
        get => timesFinished;
        set => timesFinished = value;
    }

    public string LastEndNodeId
    {
        get => lastEndNodeId;
        set => lastEndNodeId = value;
    }

    public string LastFinishedUtc
    {
        get => lastFinishedUtc;
        set => lastFinishedUtc = value;
    }

    public IReadOnlyList<string> VisitedNodeIds => visitedNodeIds;

    public DialogueProgress(string dialogueId)
    {
        this.dialogueId = dialogueId;
    }

    // Unity serialization needs a parameterless constructor too (safe to keep).
    public DialogueProgress() { }

    public void MarkNodeVisited(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;
        if (!visitedNodeIds.Contains(nodeId))
            visitedNodeIds.Add(nodeId);
    }

    public bool HasVisitedNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        return visitedNodeIds.Contains(nodeId);
    }

    public DialogueProgress Clone()
    {
        return new DialogueProgress
        {
            dialogueId = dialogueId,
            timesStarted = timesStarted,
            timesFinished = timesFinished,
            lastEndNodeId = lastEndNodeId,
            lastFinishedUtc = lastFinishedUtc,
            visitedNodeIds = new List<string>(visitedNodeIds ?? new List<string>())
        };
    }
}
