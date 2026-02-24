using System;
using UnityEngine;

public sealed class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private QuestDatabase database;

    public event Action OnQuestStateChanged;

    private QuestJournal Journal => QuestJournal.Instance;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        Hook();
        // Also listen for late-created singletons
        TimeManager.InstanceReady += HandleTimeManagerReady;
        GameManager.InstanceReady += HandleGameManagerReady;
    }

    private void OnDisable()
    {
        Unhook();
        TimeManager.InstanceReady -= HandleTimeManagerReady;
        GameManager.InstanceReady -= HandleGameManagerReady;
    }

    private void HandleTimeManagerReady(TimeManager tm)
    {
        UnhookTime();
        HookTime();
    }

    private void HandleGameManagerReady(GameManager gm)
    {
        UnhookGame();
        HookGame();
    }

    private void Hook()
    {
        HookTime();
        HookGame();
        HookStats();
    }

    private void Unhook()
    {
        UnhookTime();
        UnhookGame();
        UnhookStats();
    }

    private void HookTime()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged += HandleTimeChanged;
    }

    private void UnhookTime()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
    }

    private void HookGame()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LocationReady += HandleLocationReady;
    }

    private void UnhookGame()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LocationReady -= HandleLocationReady;
    }

    private void HookStats()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged += HandleStatsChanged;
    }

    private void UnhookStats()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged -= HandleStatsChanged;
    }

    private void HandleTimeChanged(DayOfWeek day, TimeOfDay phase)
    {
        // Any time gate might open/close
        TryAdvanceAllActive();
    }

    private void HandleLocationReady(SceneReference location)
    {
        // Location-dependent steps might complete
        TryAdvanceAllActive();
    }

    private void HandleStatsChanged()
    {
        // MinStats / money conditions might complete
        TryAdvanceAllActive();
    }

    private void TryAdvanceAllActive()
    {
        if (Journal == null) return;

        bool changed = false;
        foreach (var questId in Journal.Active)
            changed |= TryAdvanceQuest(questId);

        if (changed)
            OnQuestStateChanged?.Invoke();
    }

    // -----------------------
    // Public API
    // -----------------------

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

        Journal.MarkStarted(questId);

        var prog = Journal.GetOrCreateProgress(questId);
        prog.CurrentStepIndex = Mathf.Clamp(prog.CurrentStepIndex, 0, def.Steps.Count - 1);
        prog.ManualStepCompleted = false;

        // Auto-advance any immediately-completable steps
        TryAdvanceQuest(questId);

        OnQuestStateChanged?.Invoke();
        return true;
    }

    public bool IsActive(string questId) => Journal != null && Journal.IsActive(questId);
    public bool IsCompleted(string questId) => Journal != null && Journal.IsCompleted(questId);
    public QuestProgress GetProgress(string questId) => Journal != null ? Journal.GetOrCreateProgress(questId) : null;

    public bool CompleteManualStep(string questId)
    {
        if (!TryGetDefAndProg(questId, out var def, out var prog)) return false;
        if (!Journal.IsActive(questId)) return false;

        var step = GetCurrentStep(def, prog);
        if (step == null) return false;

        if (step.Type != QuestStepType.Manual) return false;

        prog.ManualStepCompleted = true;
        prog.LastUpdatedUtc = DateTime.UtcNow.ToString("O");

        var advanced = TryAdvanceQuest(questId);
        OnQuestStateChanged?.Invoke();
        return advanced;
    }

    public bool TryAdvanceQuest(string questId)
    {
        if (!TryGetDefAndProg(questId, out var def, out var prog)) return false;
        if (!Journal.IsActive(questId)) return false;

        bool changed = false;

        while (true)
        {
            var step = GetCurrentStep(def, prog);
            if (step == null)
            {
                Journal.MarkCompleted(questId);
                changed = true;
                break;
            }

            // Global time/phase constraints apply to any step
            if (!MeetsTimeConstraints(step))
                break;

            if (!IsStepComplete(step, prog))
                break;

            // Apply completion side effects (PayMoney, etc.)
            if (!ApplyStepCompletionEffects(step))
                break;

            // Consume step -> next
            prog.CurrentStepIndex++;
            prog.ManualStepCompleted = false;
            prog.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
            changed = true;

            if (prog.CurrentStepIndex >= def.Steps.Count)
            {
                Journal.MarkCompleted(questId);
                changed = true;
                break;
            }
        }

        if (changed)
            OnQuestStateChanged?.Invoke();

        return changed;
    }

    // -----------------------
    // Internals
    // -----------------------

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

        prog.CurrentStepIndex = Mathf.Clamp(prog.CurrentStepIndex, 0, Mathf.Max(0, def.Steps.Count - 1));
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
        if (tm == null) return true; // if time system absent, don't block

        // Day window
        if (step.MinDay >= 0 && (int)tm.DayOfWeek < step.MinDay) return false;
        if (step.MaxDay >= 0 && (int)tm.DayOfWeek > step.MaxDay) return false;

        // Phase required
        if (!string.IsNullOrEmpty(step.RequiredPhaseId))
        {
            // Expecting "Morning"/"Afternoon"/"Evening"/"Night"
            if (!string.Equals(step.RequiredPhaseId, tm.TimeOfDay.ToString(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
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
                // "Complete" means "can pay now" (payment happens in ApplyStepCompletionEffects)
                return CanPay(step);

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

    private bool IsAtTargetLocation(QuestStepDefinition step)
    {
        if (GameManager.Instance == null) return false;
        if (string.IsNullOrEmpty(step.TargetLocationSceneName)) return false;

        var cur = GameManager.Instance.CurrentLocation;
        return string.Equals(cur, step.TargetLocationSceneName, StringComparison.Ordinal);
    }

    private bool MeetsMinStats(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        if (step.MinStats == null || step.MinStats.Count == 0)
            return true; // no requirements = pass

        foreach (var req in step.MinStats)
        {
            if (req == null || string.IsNullOrEmpty(req.StatId)) continue;

            int value = GetStatValue(stats, req.StatId);
            if (value < req.MinValue)
                return false;
        }

        return true;
    }

    private int GetStatValue(PlayerStatsManager stats, string statId)
    {
        // Keep it simple for now — matches your current PlayerStatsManager fields.
        if (string.Equals(statId, "money", StringComparison.OrdinalIgnoreCase)) return stats.Money;
        if (string.Equals(statId, "strength", StringComparison.OrdinalIgnoreCase)) return stats.Strength;
        if (string.Equals(statId, "intellect", StringComparison.OrdinalIgnoreCase)) return stats.Intellect;

        // Unknown stat => fail safely
        return int.MinValue;
    }

    private bool HasMoney(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        int required = step.MinMoney > 0 ? step.MinMoney : step.Amount;
        if (required <= 0) return true;

        return stats.Money >= required;
    }

    private bool CanPay(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        int cost = step.Amount > 0 ? step.Amount : step.MinMoney;
        if (cost <= 0) return true;

        return stats.CanAfford(cost);
    }

    private bool TryPay(QuestStepDefinition step)
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return false;

        int cost = step.Amount > 0 ? step.Amount : step.MinMoney;
        if (cost <= 0) return true;

        return stats.TrySpendMoney(cost);
    }
}