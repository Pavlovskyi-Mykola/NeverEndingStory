using UnityEngine;

public class NpcInteractable : MonoBehaviour
{
    private NpcDefinition _def;
    private DialogueGraph _dialogue; // chosen from schedule entry
    private SceneReference _currentLocation;

    public void Init(NpcDefinition def, NpcScheduleEntry entry, SceneReference currentLocation)
    {
        _def = def;
        _dialogue = entry.Dialogue;           // schedule-specific dialogue
        _currentLocation = currentLocation;   // optional (debug/logging)
    }

    // Option A: Talk directly (UI button calls this)
    public void Talk()
    {
        if (_dialogue == null)
        {
            Debug.LogWarning($"NPC '{_def?.NpcId}' has no dialogue for current schedule/location.");
            return;
        }

        if (DialogueRunner.Instance == null)
        {
            Debug.LogError("DialogueRunner.Instance is null (make sure it's in Bootstrap).");
            return;
        }

        DialogueRunner.Instance.StartDialogue(_dialogue);
    }
}
