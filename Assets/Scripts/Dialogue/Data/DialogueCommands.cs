using System;
using UnityEngine;

public enum DialogueCommandType
{
    AddMoney,
    SpendMoney,
    AddStrength,
    AddIntellect,
    AdvanceTimePhase,
    SetFlag
}

[Serializable]
public class DialogueCommand
{
    public DialogueCommandType type;

    public int intValue;
    public string stringValue; // flag id
    public bool boolValue;     // flag value

    public void Execute()
    {
        switch (type)
        {
            case DialogueCommandType.AddMoney:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddMoney(intValue);
                break;

            case DialogueCommandType.SpendMoney:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.TrySpendMoney(intValue);
                break;

            case DialogueCommandType.AddStrength:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddStrength(intValue);
                break;

            case DialogueCommandType.AddIntellect:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddIntellect(intValue);
                break;

            case DialogueCommandType.AdvanceTimePhase:
                if (TimeManager.Instance != null)
                    TimeManager.Instance.AdvancePhase(TimeChangeSource.Dialogue);
                break;

            case DialogueCommandType.SetFlag:
                if (FlagsManager.Instance != null)
                    FlagsManager.Instance.SetFlag(stringValue, boolValue);
                break;
        }
    }
}
