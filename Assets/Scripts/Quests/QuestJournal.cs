using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// QuestJournal = player-facing quest state.
/// Lives in Bootstrap and persists via DontDestroyOnLoad.
/// </summary>
public sealed class QuestJournal : MonoBehaviour
{
    public static QuestJournal Instance { get; private set; }

    private readonly HashSet<string> _active = new();
    private readonly HashSet<string> _completed = new();
    private readonly Dictionary<string, QuestProgress> _progressByQuest = new();

    public IReadOnlyCollection<string> Active => _active;
    public IReadOnlyCollection<string> Completed => _completed;
    public IReadOnlyDictionary<string, QuestProgress> ProgressByQuest => _progressByQuest;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsActive(string questId) => !string.IsNullOrEmpty(questId) && _active.Contains(questId);
    public bool IsCompleted(string questId) => !string.IsNullOrEmpty(questId) && _completed.Contains(questId);

    public QuestProgress GetOrCreateProgress(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;

        if (!_progressByQuest.TryGetValue(questId, out var p) || p == null)
        {
            p = new QuestProgress(questId);
            _progressByQuest[questId] = p;
        }

        return p;
    }

    public void MarkStarted(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;

        _completed.Remove(questId);
        _active.Add(questId);

        var p = GetOrCreateProgress(questId);
        if (p != null)
        {
            p.TimesStarted++;
            p.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
        }
    }

    public void MarkCompleted(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;

        _active.Remove(questId);
        _completed.Add(questId);

        var p = GetOrCreateProgress(questId);
        if (p != null)
        {
            p.TimesCompleted++;
            p.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
        }
    }

    public void ClearAll()
    {
        _active.Clear();
        _completed.Clear();
        _progressByQuest.Clear();
    }

    // -----------------------
    // Save/Load readiness
    // -----------------------

    [Serializable]
    public class Snapshot
    {
        public List<string> active = new();
        public List<string> completed = new();
        public List<QuestProgress> progress = new();
    }

    public Snapshot CaptureSnapshot()
    {
        var snap = new Snapshot();
        snap.active.AddRange(_active);
        snap.completed.AddRange(_completed);

        foreach (var kv in _progressByQuest)
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

        if (snap.active != null)
            foreach (var id in snap.active)
                if (!string.IsNullOrEmpty(id)) _active.Add(id);

        if (snap.completed != null)
            foreach (var id in snap.completed)
                if (!string.IsNullOrEmpty(id)) _completed.Add(id);

        if (snap.progress != null)
        {
            foreach (var p in snap.progress)
            {
                if (p == null || string.IsNullOrEmpty(p.QuestId)) continue;
                _progressByQuest[p.QuestId] = p;
            }
        }
    }
}