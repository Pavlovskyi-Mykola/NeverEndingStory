using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Quest", menuName = "Game/Quests/Quest Definition")]
public sealed class QuestDefinition : ScriptableObject
{
    [Header("Identity")]
    public string QuestId;

    [Header("Display")]
    public string Title;
    [TextArea(2, 6)] public string Description;

    [Header("Steps (linear)")]
    public List<QuestStepDefinition> Steps = new();

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(QuestId)) return false;
        if (Steps == null || Steps.Count == 0) return false;
        return true;
    }
}

[Serializable]
public class QuestStepDefinition
{
    public string StepId; // optional, but helpful for debugging / future non-linear
    [TextArea(1, 4)] public string Text;

    public QuestStepType Type = QuestStepType.Manual;

    // ---- Generic payload fields (used later) ----
    [Header("Targets (optional; used by some step types later)")]
    public string TargetNpcId;
    public string TargetLocationSceneName;
    public string ItemId;
    public int Amount;

    [Header("Requirements (optional; used later)")]
    public int MinMoney;
    public List<StatRequirement> MinStats = new();

    [Header("Time gating (optional; used later)")]
    public int MinDay = -1;
    public int MaxDay = -1;
    public string RequiredPhaseId; // keep string; later map to your phase enum/id
}

[Serializable]
public class StatRequirement
{
    public string StatId;
    public int MinValue;
}