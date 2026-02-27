using System;
using UnityEngine;

[Serializable]
public sealed class QuestProgress
{
    [SerializeField] private string questId;
    [SerializeField] private int currentStepIndex;
    [SerializeField] private bool manualStepCompleted;

    [SerializeField] private int timesStarted;
    [SerializeField] private int timesCompleted;
    [SerializeField] private string lastUpdatedUtc;

    public string QuestId => questId;
    public int CurrentStepIndex { get => currentStepIndex; set => currentStepIndex = value; }
    public bool ManualStepCompleted { get => manualStepCompleted; set => manualStepCompleted = value; }

    public int TimesStarted { get => timesStarted; set => timesStarted = value; }
    public int TimesCompleted { get => timesCompleted; set => timesCompleted = value; }
    public string LastUpdatedUtc { get => lastUpdatedUtc; set => lastUpdatedUtc = value; }

    public int LastConsumedTalkToken;

    public QuestProgress(string questId)
    {
        this.questId = questId;
        currentStepIndex = 0;
        manualStepCompleted = false;
        timesStarted = 0;
        timesCompleted = 0;
        lastUpdatedUtc = DateTime.UtcNow.ToString("O");
    }

    public QuestProgress Clone()
    {
        return new QuestProgress(questId)
        {
            currentStepIndex = this.currentStepIndex,
            manualStepCompleted = this.manualStepCompleted,
            timesStarted = this.timesStarted,
            timesCompleted = this.timesCompleted,
            lastUpdatedUtc = this.lastUpdatedUtc
        };
    }
}