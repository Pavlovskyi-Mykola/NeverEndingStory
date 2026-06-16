using UnityEngine;

/// <summary>
/// World interaction that sleeps until next morning. Place on a bed/couch object
/// in the Home scene and wire Sleep() to its Button's OnClick (same pattern as
/// NpcInteractable). Living in the Home scene means it's only available there —
/// no global HUD gating needed.
/// </summary>
public sealed class SleepPoint : MonoBehaviour
{
    public void Sleep()
    {
        // TimeManager.SleepToMorning already ignores PlayerUI input during dialogue,
        // but block while any blocking UI panel is open too (inventory/journal/etc.)
        // since a world-space button isn't covered by the UI backdrop.
        if (UIPanelManager.IsGameplayBlocked) return;

        if (TimeManager.Instance == null) return;
        TimeManager.Instance.SleepToMorning(TimeChangeSource.PlayerUI);
    }
}
