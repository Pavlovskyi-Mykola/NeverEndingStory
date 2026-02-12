using UnityEngine;

public class NpcDialogueLauncher : MonoBehaviour
{
    [Header("Routing")]
    [SerializeField] private string npcId;
    [SerializeField] private string locationId;
    [SerializeField] private DialogueRouteSet routes;

    public void TryStartDialogue()
    {
        if (DialogueRunner.Instance == null) return;
        if (routes == null) return;

        var ctx = DialogueSelectorContext.From(npcId, locationId);
        var graph = DialogueSelector.Select(routes, ctx);

        if (graph == null)
        {
            Debug.LogWarning($"[NpcDialogueLauncher] No dialogue graph selected for npc='{npcId}' location='{locationId}'.");
            return;
        }

        DialogueRunner.Instance.StartDialogue(graph);
    }
}
