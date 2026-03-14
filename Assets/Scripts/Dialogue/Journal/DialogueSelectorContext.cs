using System;
using UnityEngine;

[Serializable]
public struct DialogueSelectorContext
{
    public string NpcId;
    public string LocationId;

    public DayOfWeek Day;
    public TimeOfDay Phase;

    public int Money;
    public int Strength;
    public int Intellect;

    public static DialogueSelectorContext From(string npcId, string locationId)
    {
        return new DialogueSelectorContext
        {
            NpcId = npcId,
            LocationId = locationId,
            Day = TimeManager.Instance != null ? TimeManager.Instance.DayOfWeek : DateTime.Now.DayOfWeek,
            Phase = TimeManager.Instance != null ? TimeManager.Instance.TimeOfDay : TimeOfDay.Morning,
            Money = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Money : 0,
            Strength = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Strength : 0,
            Intellect = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Intellect : 0
        };
    }

    public QuestRouteState GetQuestState(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return QuestRouteState.Any;

        var journal = QuestJournal.Instance;
        if (journal == null)
            return QuestRouteState.NotStarted;

        if (journal.IsActive(questId))
            return QuestRouteState.Active;

        if (journal.IsCompleted(questId))
            return QuestRouteState.Completed;

        return QuestRouteState.NotStarted;
    }

    public bool IsQuestOnStepId(string questId, string requiredStepId)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(requiredStepId))
            return false;

        var journal = QuestJournal.Instance;
        var questManager = QuestManager.Instance;
        if (journal == null || questManager == null)
            return false;

        if (!journal.IsActive(questId))
            return false;

        if (!questManager.TryGetDefinition(questId, out var def) || def == null || def.Steps == null || def.Steps.Count == 0)
            return false;

        var prog = journal.GetOrCreateProgress(questId);
        if (prog == null)
            return false;

        int idx = prog.CurrentStepIndex;
        if (idx < 0 || idx >= def.Steps.Count)
            return false;

        var step = def.Steps[idx];
        if (step == null)
            return false;

        return string.Equals(step.StepId, requiredStepId, StringComparison.Ordinal);
    }

    public bool IsQuestOnStepIndex(string questId, int requiredStepIndex)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return false;

        if (requiredStepIndex < 0)
            return false;

        var journal = QuestJournal.Instance;
        if (journal == null)
            return false;

        if (!journal.IsActive(questId))
            return false;

        var prog = journal.GetOrCreateProgress(questId);
        if (prog == null)
            return false;

        return prog.CurrentStepIndex == requiredStepIndex;
    }

    public bool CheckFlag(string flagId, bool expectedValue)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return true;

        if (WorldState.Instance == null)
            return false;

        return WorldState.Instance.GetFlag(flagId, false) == expectedValue;
    }
}