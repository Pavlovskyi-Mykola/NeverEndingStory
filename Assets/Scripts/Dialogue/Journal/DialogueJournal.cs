using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DialogueJournal = player-facing dialogue state.
/// Lives in Bootstrap and persists via DontDestroyOnLoad.
/// </summary>
public sealed class DialogueJournal : MonoBehaviour
{
    public static DialogueJournal Instance { get; private set; }

    // ---- Runtime state (fast lookups) ----
    private readonly HashSet<string> _seenDialogues = new();
    private readonly Dictionary<string, DialogueProgress> _progressByDialogue = new();

    private readonly HashSet<string> _seenLines = new();
    private readonly HashSet<string> _seenChoices = new();

    // ---- Read-only views ----
    public IReadOnlyCollection<string> SeenDialogues => _seenDialogues;
    public IReadOnlyDictionary<string, DialogueProgress> ProgressByDialogue => _progressByDialogue;
    public IReadOnlyCollection<string> SeenLines => _seenLines;
    public IReadOnlyCollection<string> SeenChoices => _seenChoices;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -----------------------
    // Public API (marking)
    // -----------------------

    public bool HasSeenDialogue(string dialogueId) => !string.IsNullOrEmpty(dialogueId) && _seenDialogues.Contains(dialogueId);
    public bool HasSeenLine(string lineId) => !string.IsNullOrEmpty(lineId) && _seenLines.Contains(lineId);
    public bool HasSeenChoice(string choiceId) => !string.IsNullOrEmpty(choiceId) && _seenChoices.Contains(choiceId);

    public void MarkDialogueSeen(string dialogueId)
    {
        if (string.IsNullOrEmpty(dialogueId)) return;
        _seenDialogues.Add(dialogueId);
    }

    public void MarkLineSeen(string lineId)
    {
        if (string.IsNullOrEmpty(lineId)) return;
        _seenLines.Add(lineId);
    }

    public void MarkChoiceSeen(string choiceId)
    {
        if (string.IsNullOrEmpty(choiceId)) return;
        _seenChoices.Add(choiceId);
    }

    /// <summary>
    /// Get or create per-dialogue progress container.
    /// </summary>
    public DialogueProgress GetOrCreateProgress(string dialogueId)
    {
        if (string.IsNullOrEmpty(dialogueId)) return null;

        if (!_progressByDialogue.TryGetValue(dialogueId, out var prog) || prog == null)
        {
            prog = new DialogueProgress(dialogueId);
            _progressByDialogue[dialogueId] = prog;
        }

        return prog;
    }

    // -----------------------
    // Optional helpers for Runner hooks
    // -----------------------

    public void OnDialogueStarted(string dialogueId)
    {
        MarkDialogueSeen(dialogueId);
        var p = GetOrCreateProgress(dialogueId);
        if (p != null) p.TimesStarted++;
    }

    public void OnDialogueFinished(string dialogueId, string endNodeId = null)
    {
        if (string.IsNullOrEmpty(dialogueId)) return;

        var p = GetOrCreateProgress(dialogueId);
        if (p != null)
        {
            p.TimesFinished++;
            p.LastEndNodeId = endNodeId;
            p.LastFinishedUtc = DateTime.UtcNow.ToString("O"); // ISO 8601
        }
    }

    // -----------------------
    // Reset / New Game
    // -----------------------

    public void ClearAll()
    {
        _seenDialogues.Clear();
        _progressByDialogue.Clear();
        _seenLines.Clear();
        _seenChoices.Clear();
    }

    // -----------------------
    // Save/Load readiness
    // -----------------------

    [Serializable]
    public class Snapshot
    {
        public List<string> seenDialogues = new();
        public List<DialogueProgress> progress = new();
        public List<string> seenLines = new();
        public List<string> seenChoices = new();
    }

    public Snapshot CaptureSnapshot()
    {
        var snap = new Snapshot();

        snap.seenDialogues.AddRange(_seenDialogues);
        snap.seenLines.AddRange(_seenLines);
        snap.seenChoices.AddRange(_seenChoices);

        foreach (var kv in _progressByDialogue)
        {
            if (kv.Value != null)
                snap.progress.Add(kv.Value.Clone());
        }

        return snap;
    }

    public void RestoreSnapshot(Snapshot snap)
    {
        ClearAll();
        if (snap == null) return;

        if (snap.seenDialogues != null)
            foreach (var id in snap.seenDialogues)
                if (!string.IsNullOrEmpty(id)) _seenDialogues.Add(id);

        if (snap.seenLines != null)
            foreach (var id in snap.seenLines)
                if (!string.IsNullOrEmpty(id)) _seenLines.Add(id);

        if (snap.seenChoices != null)
            foreach (var id in snap.seenChoices)
                if (!string.IsNullOrEmpty(id)) _seenChoices.Add(id);

        if (snap.progress != null)
        {
            foreach (var p in snap.progress)
            {
                if (p == null || string.IsNullOrEmpty(p.DialogueId)) continue;
                _progressByDialogue[p.DialogueId] = p;
            }
        }
    }
}
