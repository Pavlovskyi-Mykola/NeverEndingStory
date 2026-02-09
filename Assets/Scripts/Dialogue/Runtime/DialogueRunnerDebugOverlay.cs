using UnityEngine;

public class DialogueRunnerDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool visible = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible) return;

        var r = DialogueRunner.Instance;
        if (r == null)
        {
            GUI.Label(new Rect(10, 10, 500, 60), "DialogueRunner: (missing)");
            return;
        }

        string status = r.IsRunning ? "RUNNING" : "IDLE";
        string advancing = r.IsAdvancing ? "ADVANCING (auto)" : "WAITING (input)";
        string nodeInfo = r.DebugCurrentNodeInfo; // we’ll add this property in runner below

        GUI.Box(new Rect(10, 10, 520, 90), "");
        GUI.Label(new Rect(20, 20, 500, 20), $"DialogueRunner: {status} | {advancing}");
        GUI.Label(new Rect(20, 42, 500, 20), $"Node: {nodeInfo}");
        GUI.Label(new Rect(20, 64, 500, 20), $"Toggle: {toggleKey}");
    }
}
