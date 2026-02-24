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

        // If already completed, don't restart (you can change this rule later)
        if (Journal.IsCompleted(questId))
            return false;

        Journal.MarkStarted(questId);

        // Ensure progress exists and is within bounds
        var prog = Journal.GetOrCreateProgress(questId);
        prog.CurrentStepIndex = Mathf.Clamp(prog.CurrentStepIndex, 0, def.Steps.Count - 1);
        prog.ManualStepCompleted = false;

        // Try to advance immediately (AutoComplete chains)
        TryAdvanceQuest(questId);

        OnQuestStateChanged?.Invoke();
        return true;
    }

    public bool IsActive(string questId) => Journal != null && Journal.IsActive(questId);
    public bool IsCompleted(string questId) => Journal != null && Journal.IsCompleted(questId);

    public QuestProgress GetProgress(string questId) => Journal != null ? Journal.GetOrCreateProgress(questId) : null;

    /// <summary>
    /// For StepType.Manual: call this when some external logic decides the step is done.
    /// (Later: Dialogue/Inventory/Time hooks will call TryAdvanceQuest automatically.)
    /// </summary>
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

    /// <summary>
    /// Evaluate + advance as far as possible (useful when conditions change).
    /// Returns true if anything advanced/completed.
    /// </summary>
    public bool TryAdvanceQuest(string questId)
    {
        if (!TryGetDefAndProg(questId, out var def, out var prog)) return false;
        if (!Journal.IsActive(questId)) return false;

        bool changed = false;

        // Advance through any completed steps (including chained AutoComplete)
        while (true)
        {
            var step = GetCurrentStep(def, prog);
            if (step == null)
            {
                // No steps => complete
                Journal.MarkCompleted(questId);
                changed = true;
                break;
            }

            if (!IsStepComplete(step, prog))
                break;

            // Step complete -> next
            prog.CurrentStepIndex++;
            prog.ManualStepCompleted = false;
            prog.LastUpdatedUtc = DateTime.UtcNow.ToString("O");
            changed = true;

            // Quest complete?
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

        // Keep safe bounds
        prog.CurrentStepIndex = Mathf.Clamp(prog.CurrentStepIndex, 0, Mathf.Max(0, def.Steps.Count - 1));
        return true;
    }

    private static QuestStepDefinition GetCurrentStep(QuestDefinition def, QuestProgress prog)
    {
        if (def == null || prog == null || def.Steps == null) return null;
        if (prog.CurrentStepIndex < 0 || prog.CurrentStepIndex >= def.Steps.Count) return null;
        return def.Steps[prog.CurrentStepIndex];
    }

    private static bool IsStepComplete(QuestStepDefinition step, QuestProgress prog)
    {
        if (step == null || prog == null) return false;

        switch (step.Type)
        {
            case QuestStepType.AutoComplete:
                return true;

            case QuestStepType.Manual:
                return prog.ManualStepCompleted;

            // For now: future step types return false until we add integrations
            default:
                return false;
        }
    }
}