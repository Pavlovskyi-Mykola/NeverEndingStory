using UnityEngine;

public class DialogueDebugStart : MonoBehaviour
{
    [SerializeField] private DialogueGraph graphToTest;

    public void StartDialogue()
    {
        if (graphToTest == null)
        {
            Debug.LogWarning("DialogueDebugStart: No graph assigned.");
            return;
        }

        if (DialogueRunner.Instance == null)
        {
            Debug.LogError("DialogueDebugStart: DialogueRunner.Instance is null. Make sure DialogueRunner is in Bootstrap scene and enabled.");
            return;
        }

        DialogueRunner.Instance.StartDialogue(graphToTest);
    }
}
