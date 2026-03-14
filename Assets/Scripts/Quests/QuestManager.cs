using System;
using UnityEngine;

public sealed class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }
    public static event Action<QuestManager> InstanceReady;

    [Header("Data")]
    [SerializeField] private QuestDatabase database;

    public event Action OnQuestStateChanged;

    private int _talkToken = 0;
    private string _lastTalkNpcId = null;
    private string _lastTalkDialogueId = null;

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
        TryAdvanceAllActive();
    }

    private void HandleNpcTalked(string npcId, string dialogueId)
    {
        if (string.IsNullOrEmpty(npcId) && string.IsNullOrEmpty(dialogueId))
            return;

        _talkToken++;
        _lastTalkNpcId = npcId;
        _lastTalkDialogueId = dialogueId;

        TryAdvanceAllActive();
    }

    private void HandleTimeChanged(DayOfWeek day, TimeOfDay phase, TimeChangeSource source)
    {
        TryAdvanceAllActive();
    }

    private void HandleLocationEntered(string locationSceneName)
    {
        TryAdvanceAllActive();
    }

    private void HandleStatsChanged(GameEvents.StatsSnapshot snapshot)
    {
        TryAdvanceAllActive();
    }

    private void TryAdvanceAllActive()
    {
        if (Journal == null) return;

        bool changed = false;

        var snapshot = new System.Collections.Generic.List<string>(Journal.Active);

        for (int i = 0; i < snapshot.Count; i++)
        {
            var questId = snapshot[i];
            if (!Journal.IsActive(questId)) continue;

            changed |= TryAdvanceQuest(questId);
        }

        if (changed)
            OnQuestStateChanged?.Invoke();
    }

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

        if (Journal.IsCompleted(questId))
            return false;

        if (Journal.IsActive(questId))
            return false;

        Journal.MarkStarted(questId);

        var prog = Journal.GetOrCreateProgress(questId);
        prog.CurrentStepIndex = Mathf.Clamp(prog.CurrentStepIndex, 0, def.Steps.Count - 1);
        prog.ManualStepCompleted = false;
        prog.CompletionRewardsGranted = false;

        TryAdvanceQuest(questId);

        OnQuestStateChanged?.Invoke();
        return true;
    }

    public bool TryAdvanceQuest(string questId)
    {
        if (!TryGetDefAndProg(questId, out var def, out var prog)) return false;
        if (!Journal.IsActive(questId)) return false;

        bool changed = false;

        while (true)
        {
            if (prog.CurrentStepIndex >= def.Steps.Count)
            {
                Journal.MarkCompleted(questId);
                changed = true;
                break;
            }

            var step = GetCurrentStep(def, prog);
            if (step == null)
            {
                GrantCompletionRewards(def, prog);
                Journal.MarkCompleted(questId);
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
                GrantCompletionRewards(def, prog);
                Journal.MarkCompleted(questId);
                changed = true;
                break;
            }
        }

        if (changed)
            OnQuestStateChanged?.Invoke();

        return changed;
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

        if (step.RequiredStrength > 0 && stats.Strength < step.RequiredStrength) return false;
        if (step.RequiredIntellect > 0 && stats.Intellect < step.RequiredIntellect) return false;

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
        TryAdvanceAllActive();
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