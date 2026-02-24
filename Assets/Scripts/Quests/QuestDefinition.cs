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
    [Header("Meta")]
    public string StepId; // optional but helpful for debugging
    [TextArea(1, 4)] public string Text;

    public QuestStepType Type = QuestStepType.Manual;

    [Header("Location (ReachLocation)")]
    public SceneReference TargetLocation;

    [Header("Time restrictions (optional; gates ANY step)")]
    public bool RestrictByDay = false;
    public DayOfWeek[] AllowedDays;

    public bool RestrictByPhase = false;
    public TimeOfDay[] AllowedPhases;

    [Header("Stats requirements (MinStats)")]
    public int RequiredStrength = 0;
    public int RequiredIntellect = 0;

    [Header("Money (HaveMoney / PayMoney)")]
    public int RequiredMoney = 0;
}