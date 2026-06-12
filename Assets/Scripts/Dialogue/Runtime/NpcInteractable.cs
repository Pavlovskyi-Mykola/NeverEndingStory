using UnityEngine;

public class NpcInteractable : MonoBehaviour
{
    private NpcDialogueLauncher _launcher;

    private void Awake()
    {
        _launcher = GetComponent<NpcDialogueLauncher>();
    }

    public void Talk()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsInDialogue)
            return;

        if (UIPanelManager.IsGameplayBlocked)
            return;

        if (_launcher == null)
        {
            Debug.LogError("[NpcInteractable] Missing NpcDialogueLauncher component on NPC prefab.");
            return;
        }

        _launcher.TryStartDialogue();
    }
}

