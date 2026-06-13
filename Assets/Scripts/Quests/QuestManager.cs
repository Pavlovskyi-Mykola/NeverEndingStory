using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }
    public static event Action<QuestManager> InstanceReady;

    [Header("Data")]
    [SerializeField] private QuestDatabase database;

    /// <summary>Coarse signal: something about quest state changed. UI rebuilds on it.</summary>
    public event Action OnQuestStateChanged;

    // Granular signals for notifications/analytics. Fired after the batch settles,
    // so listeners always see consistent state and can't re-enter the advance loop.
    public event Action<string> OnQuestStarted;            // questId
    public event Action<string, int> OnQuestStepAdvanced;  // questId, new step index
    public event Action<string> OnQuestCompleted;          // questId

    private int _talkToken = 0;
    private string _lastTalkNpcId = null;
    private string _lastTalkDialogueId = null;

    // Game events mark quests dirty; advancement runs once per frame in LateUpdate.
    // This collapses event bursts (e.g. a reward setting several flags) into a single
    // pass and keeps the advance loop from re-entering itself when completing a
    // quest raises events (rewards -> FlagChanged/StatsChanged -> dirty again).
    private bool _advancePending;
    private bool _processingBatch;

    // Quests completed during the current batch — prevents a repeatable quest with
    // a 0-phase cooldown from instantly restarting and looping within one frame.
    private readonly HashSet<string> _completedThisBatch = new();

    private enum QuestEventKind { Started, StepAdvanced, Completed }
    private struct QuestEvent { public QuestEventKind Kind; public string QuestId; public int StepIndex; }
    private readonly List<QuestEvent> _pendingEvents = new();

    private QuestJournal Journal => QuestJournal.Instance;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InstanceReady?.Invoke(this);
    }

    private void OnEnable()
    {
        // One unified event surface.
        GameEvents.TimeChanged += HandleTimeChanged;
        GameEvents.LocationEntered += HandleLocationEntered;
        GameEvents.StatsChanged += HandleStatsChanged;
        GameEvents.NpcTalked += HandleNpcTalked;
        GameEvents.FlagChanged += HandleFlagChanged;
        GameEvents.InventoryChanged += HandleInventoryChanged;
    }

    private void OnDisable()
    {
        GameEvents.TimeChanged -= HandleTimeChanged;
        GameEvents.LocationEntered -= HandleLocationEntered;
        GameEvents.StatsChanged -= HandleStatsChanged;
        GameEvents.NpcTalked -= HandleNpcTalked;
        GameEvents.FlagChanged -= HandleFlagChanged;
        GameEvents.InventoryChanged -= HandleInventoryChanged;
    }

    //flags become a first-class quest trigger
    private void HandleFlagChanged(string key, bool value)
    {
        MarkQuestsDirty();
    }

    private void HandleNpcTalked(string npcId, string dialogueId)
    {
        if (string.IsNullOrEmpty(npcId) && string.IsNullOrEmpty(dialogueId))
            return;

        _talkToken++;
        _lastTalkNpcId = npcId;
        _lastTalkDialogueId = dialogueId;

        MarkQuestsDirty();
    }

    private void HandleTimeChanged(DayOfWeek day, TimeOfDay phase, TimeChangeSource source)
    {
        MarkQuestsDirty();
    }

    private void HandleLocationEntered(string locationSceneName)
    {
        MarkQuestsDirty();
    }

    private void HandleStatsChanged(GameEvents.StatsSnapshot snapshot)
    {
        MarkQuestsDirty();
    }

    private void MarkQuestsDirty()
    {
        _advancePending = true;
    }

    private void LateUpdate()
    {
        if (!_advancePending || _processingBatch)
            return;

        _processingBatch = true;
        _completedThisBatch.Clear();
        bool changed = false;

        // Advancing can raise events that mark quests dirty again (rewards setting
        // flags) and auto-start can spawn quests that then need advancing; keep
        // going so chains resolve within the same frame, capped against runaways.
        int pass = 0;
        while (_advancePending && pass++ < 8)
        {
            _advancePending = false;
            changed |= AdvanceActiveQuests();
            changed |= EvaluateAutoStarts();
        }

        if (_advancePending)
            Debug.LogWarning("[QuestManager] Quest advancement did not settle after 8 passes — check for quests that repeatedly toggle the same flag, or a 0-cooldown repeatable auto-start.");

        _processingBatch = false;

        // One coarse event per batch; granular events flushed after state is settled.
        if (changed)
            OnQuestStateChanged?.Invoke();

        FlushQuestEvents();
    }

    private bool AdvanceActiveQuests()
    {
        if (Journal == null) return false;

        bool changed = false;
        var snapshot = new List<string>(Journal.Active);

        for (int i = 0; i < snapshot.Count; i++)
        {
            var questId = snapshot[i];
            if (!Journal.IsActive(questId)) continue;

            changed |= AdvanceQuestNoNotify(questId);
        }

        return changed;
    }

    private bool EvaluateAutoStarts()
    {
        if (Journal == null || database == null || database.Quests == null)
            return false;

        bool startedAny = false;
        var quests = database.Quests;

        for (int i = 0; i < quests.Count; i++)
        {
            var def = quests[i];
            if (def == null || !def.AutoStart || !def.IsValid())
                continue;

            if (!CanStart(def))
                continue;

            if (def.StartCondition != null && !def.StartCondition.IsMet(Journal))
                continue;

            if (StartQuestInternal(def.QuestId))
            {
                startedAny = true;
                _advancePending = true; // re-run so the new quest advances this batch
            }
        }

        return startedAny;
    }

    /// <summary>Public entry (dialogue commands, debug). Starts immediately and flushes its own events.</summary>
    public bool StartQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return false;
        if (Journal == null) { Debug.LogWarning("[QuestManager] QuestJournal missing in scene."); return false; }
        if (database == null) { Debug.LogWarning("[QuestManager] QuestDatabase not assigned."); return false; }

        if (!database.TryGet(questId, out var def) || def == null || !def.IsValid())
        {
            Debug.LogWarning($"[QuestManager] Cannot start quest '{questId}' (missing/invalid definition).");
            return false;
        }

        bool started = StartQuestInternal(questId);

        // When called from inside the batch, let it own notify/flush. Otherwise
        // mark dirty so the next batch can run auto-starts/chains off this quest.
        if (!_processingBatch)
        {
            if (started)
            {
                OnQuestStateChanged?.Invoke();
                MarkQuestsDirty();
            }
            FlushQuestEvents();
        }

        return started;
    }

    private bool StartQuestInternal(string questId)
    {
        if (!CanStart(questId, out _))
            return false;

        Journal.MarkStarted(questId); // removes from completed, adds active, TimesStarted++

        var prog = Journal.GetOrCreateProgress(questId);
        prog.CurrentStepIndex = 0;
        prog.ManualStepCompleted = false;
        prog.CompletionRewardsGranted = false;

        // Talks that happened before this quest existed must not satisfy its
        // TalkToDialogue steps — consume the current token up front.
        prog.LastConsumedTalkToken = _talkToken;

        QueueEvent(QuestEventKind.Started, questId, 0);

        AdvanceQuestNoNotify(questId);
        return true;
    }

    private bool CanStart(QuestDefinition def) =>
        def != null && CanStart(def.QuestId, out _);

    private bool CanStart(string questId, out QuestDefinition def)
    {
        def = null;
        if (string.IsNullOrEmpty(questId) || Journal == null || database == null)
            return false;

        if (!database.TryGet(questId, out def) || def == null || !def.IsValid())
            return false;

        if (Journal.IsActive(questId))
            return false;

        // Don't restart something that just completed this batch (0-cooldown loop guard).
        if (_completedThisBatch.Contains(questId))
            return false;

        if (Journal.IsCompleted(questId))
        {
            if (!def.Repeatable)
                return false;

            if (!IsRepeatCooldownElapsed(questId, def))
                return false;
        }

        return true;
    }

    private bool IsRepeatCooldownElapsed(string questId, QuestDefinition def)
    {
        if (def.RepeatCooldownPhases <= 0)
            return true;

        var prog = Journal.GetOrCreateProgress(questId);
        if (prog == null || prog.LastCompletedPhase < 0)
            return true;

        long now = TimeManager.Instance != null ? TimeManager.Instance.TotalPhasesElapsed : 0;
        return now - prog.LastCompletedPhase >= def.RepeatCooldownPhases;
    }

    public bool TryAdvanceQuest(string questId)
    {
        bool changed = AdvanceQuestNoNotify(questId);

        if (!_processingBatch)
        {
            if (changed)
            {
                OnQuestStateChanged?.Invoke();
                MarkQuestsDirty();
            }
            FlushQuestEvents();
        }

        return changed;
    }

    private bool AdvanceQuestNoNotify(string questId)
    {
        if (!TryGetDefAndProg(questId, out var def, out var prog)) return false;
        if (!Journal.IsActive(questId)) return false;

        bool changed = false;

        while (true)
        {
            if (prog.CurrentStepIndex >= def.Steps.Count)
            {
                CompleteQuest(questId, def, prog);
                changed = true;
                break;
            }

            var step = GetCurrentStep(def, prog);
            if (step == null)
            {
                CompleteQuest(questId, def, prog);
                changed = true;
                break;
            }

            if (!MeetsTimeConstraints(step))
                break;

            if (!IsStepComplete(step, prog))
                break;

            if (!ApplyStepCompletionEffects(step))
                break;

            if (step.Type == QuestStepType.TalkToDialogue)
                prog.LastConsumedTalkToken = _talkToken;

            prog.CurrentStepIndex++;
            prog.ManualStepCompleted = false;
            prog.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
            changed = true;

            if (prog.CurrentStepIndex >= def.Steps.Count)
            {
                CompleteQuest(questId, def, prog);
                break;
            }

            QueueEvent(QuestEventKind.StepAdvanced, questId, prog.CurrentStepIndex);
        }

        return changed;
    }

    private void CompleteQuest(string questId, QuestDefinition def, QuestProgress prog)
    {
        GrantCompletionRewards(def, prog);
        Journal.MarkCompleted(questId);

        prog.LastCompletedPhase = TimeManager.Instance != null ? TimeManager.Instance.TotalPhasesElapsed : 0;
        _completedThisBatch.Add(questId);

        QueueEvent(QuestEventKind.Completed, questId, -1);
    }

    private void QueueEvent(QuestEventKind kind, string questId, int stepIndex)
    {
        _pendingEvents.Add(new QuestEvent { Kind = kind, QuestId = questId, StepIndex = stepIndex });
    }

    private void FlushQuestEvents()
    {
        if (_pendingEvents.Count == 0) return;

        // Snapshot first: a listener could trigger more quest changes mid-dispatch.
        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();

        for (int i = 0; i < events.Length; i++)
        {
            var e = events[i];
            switch (e.Kind)
            {
                case QuestEventKind.Started:     OnQuestStarted?.Invoke(e.QuestId); break;
                case QuestEventKind.StepAdvanced: OnQuestStepAdvanced?.Invoke(e.QuestId, e.StepIndex); break;
                case QuestEventKind.Completed:   OnQuestCompleted?.Invoke(e.QuestId); break;
            }
        }
    }

    private bool TryGetDefAndProg(string questId, out QuestDefinition def, out QuestProgress prog)
    {
        def = null;
        prog = null;

        if (string.IsNullOrEmpty(questId)) return false;
        if (Journal == null) return false;
        if (database == null) return false;

        if (!database.TryGet(questId, out def) || def == null || !def.IsValid())
            return false;

        prog = Journal.GetOrCreateProgress(questId);
        if (prog == null) return false;

        if (prog.CurrentStepIndex < 0) prog.CurrentStepIndex = 0;
        return true;
    }

    private static QuestStepDefinition GetCurrentStep(QuestDefinition def, QuestProgress prog)
    {
        if (def == null || prog == null || def.Steps == null) return null;
        if (prog.CurrentStepIndex < 0 || prog.CurrentStepIndex >= def.Steps.Count) return null;
        return def.Steps[prog.CurrentStepIndex];
    }

    private bool MeetsTimeConstraints(QuestStepDefinition step)
    {
        if (step == null) return true;

        var tm = TimeManager.Instance;
        if (tm == null) return true;

        if (step.RestrictByDay)
        {
            if (step.AllowedDays == DayOfWeekMask.None)
                return false;

            var currentMask = DayOfWeekMaskExtensions.From(tm.DayOfWeek);

            if ((step.AllowedDays & currentMask) == 0)
                return false;
        }

        if (step.RestrictByPhase)
        {
            if (step.AllowedPhases == DayPhaseMask.None)
                return false;

            var currentMask = DayPhaseMaskExtensions.From(tm.TimeOfDay);

            if ((step.AllowedPhases & currentMask) == 0)
                return false;
        }

        return true;
    }

    private bool IsAtTargetLocation(QuestStepDefinition step)
    {
        if (GameManager.Instance == null) return false;
        if (string.IsNullOrEmpty(step.TargetLocationSceneName)) return false;

        return string.Equals(
            GameManager.Instance.CurrentLocation,
            step.TargetLocationSceneName,
            StringComparison.Ordinal
        );
    }

    private bool MeetsMinStats(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        if (step.RequiredInfluence > 0 && stats.Influence < step.RequiredInfluence) return false;
        if (step.RequiredStrategy > 0 && stats.Strategy < step.RequiredStrategy) return false;
        if (step.RequiredNetworking > 0 && stats.Networking < step.RequiredNetworking) return false;
        if (step.RequiredReputation > 0 && stats.Reputation < step.RequiredReputation) return false;

        return true;
    }

    private bool HasMoney(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        int required = Mathf.Max(0, step.RequiredMoney);
        if (required == 0) return true;

        return stats.Money >= required;
    }

    private bool CanPay(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        int cost = Mathf.Max(0, step.RequiredMoney);
        if (cost == 0) return true;

        return stats.CanAfford(cost);
    }

    private bool TryPay(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        int cost = Mathf.Max(0, step.RequiredMoney);
        if (cost == 0) return true;

        return stats.TrySpendMoney(cost);
    }

    private bool IsStepComplete(QuestStepDefinition step, QuestProgress prog)
    {
        if (step == null || prog == null) return false;

        switch (step.Type)
        {
            case QuestStepType.AutoComplete:
                return true;

            case QuestStepType.Manual:
                return prog.ManualStepCompleted;

            case QuestStepType.ReachLocation:
                return IsAtTargetLocation(step);

            case QuestStepType.MinStats:
                return MeetsMinStats(step);

            case QuestStepType.HaveMoney:
                return HasMoney(step);

            case QuestStepType.PayMoney:
                return CanPay(step);
            case QuestStepType.HaveItem:
                return HasRequiredItem(step);

            case QuestStepType.TimeWindow:
                // MeetsTimeConstraints already gates on the window; if we reach here it's open
                return true;

            case QuestStepType.FlagTrue:
                return !string.IsNullOrWhiteSpace(step.RequiredFlagId) && WorldFlags.Get(step.RequiredFlagId);

            case QuestStepType.TalkToDialogue:
                {
                    if (prog.LastConsumedTalkToken == _talkToken)
                        return false;

                    if (!string.IsNullOrEmpty(step.TargetNpcId) &&
                        !string.Equals(step.TargetNpcId, _lastTalkNpcId, StringComparison.Ordinal))
                        return false;

                    if (string.IsNullOrEmpty(step.TargetDialogueId))
                        return false;

                    return string.Equals(step.TargetDialogueId, _lastTalkDialogueId, StringComparison.Ordinal);
                }

            default:
                return false;
        }
    }

    private bool ApplyStepCompletionEffects(QuestStepDefinition step)
    {
        if (step == null) return true;

        switch (step.Type)
        {
            case QuestStepType.PayMoney:
                return TryPay(step);

            default:
                return true;
        }
    }

    //UI show info
    public bool TryGetDefinition(string questId, out QuestDefinition def)
    {
        def = null;
        if (database == null) return false;
        return database.TryGet(questId, out def) && def != null;
    }
    private void GrantCompletionRewards(QuestDefinition def, QuestProgress prog)
    {
        if (def == null || prog == null) return;
        if (prog.CompletionRewardsGranted) return;

        if (def.CompletionRewards != null)
        {
            for (int i = 0; i < def.CompletionRewards.Count; i++)
            {
                var reward = def.CompletionRewards[i];
                if (reward == null) continue;
                reward.Grant();
            }
        }

        prog.CompletionRewardsGranted = true;
        prog.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
    }

    // --- QUESTS ---
    private void HandleInventoryChanged(GameEvents.InventoryChange change)
    {
        MarkQuestsDirty();
    }
    private bool HasRequiredItem(QuestStepDefinition step)
    {
        var inventory = InventoryManager.Instance;
        if (inventory == null) return false;
        if (step == null) return false;
        if (step.RequiredItem == null) return false;
        if (string.IsNullOrWhiteSpace(step.RequiredItem.ItemId)) return false;

        int required = Mathf.Max(1, step.RequiredItem.Count);
        return inventory.HasItem(step.RequiredItem.ItemId, required);
    }
}