using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueRunner : MonoBehaviour
{
    public static DialogueRunner Instance { get; private set; }

    public event Action<string, string> OnShowLine; // speaker, text
    public event Action<List<PresentedChoice>> OnShowChoices;
    public event Action OnHideDialogue;

    private DialogueGraph _graph;
    private DialogueNode _current;

    public event Action<bool> OnDialogueStateChanged;
    public bool IsRunning => _graph != null && _current != null;

    /// <summary>
    /// True while runner is traversing nodes (auto-continue via Branch/Command/GoTo).
    /// While true, Continue/Choose calls are ignored.
    /// </summary>
    public bool IsAdvancing { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartDialogue(DialogueGraph graph)
    {
        if (graph == null) return;

        _graph = graph;
        _current = _graph.GetNode(_graph.StartNodeId);

        if (_current == null)
        {
            Debug.LogWarning($"DialogueGraph '{graph.name}' has invalid StartNodeId.");
            StopDialogue(); // will fire false
            return;
        }

        OnDialogueStateChanged?.Invoke(true);

        // We are about to traverse until we hit an input node (Line/Choice) or End.
        IsAdvancing = true;
        EnterNode(_current);
    }

    public void StopDialogue()
    {
        _graph = null;
        _current = null;

        IsAdvancing = false;

        OnDialogueStateChanged?.Invoke(false);
        OnHideDialogue?.Invoke();
    }

    public void Continue()
    {
        if (!IsRunning) return;
        if (IsAdvancing) return; // 🔒 ignore double-advance during traversal

        if (_current is LineNode line)
        {
            IsAdvancing = true;
            GoTo(line.NextNodeId);
            return;
        }

        if (_current is CommandNode cmd)
        {
            IsAdvancing = true;
            GoTo(cmd.NextNodeId);
            return;
        }

        // Choice nodes require Choose(index)
        // End nodes will auto-stop
    }

    public void Choose(int presentedChoiceIndex)
    {
        if (!IsRunning) return;
        if (IsAdvancing) return;
        if (!(_current is ChoiceNode choiceNode)) return;

        var presented = BuildPresentedChoices(choiceNode);
        if (presentedChoiceIndex < 0 || presentedChoiceIndex >= presented.Count) return;

        var chosen = presented[presentedChoiceIndex];

        // From this moment we start traversing again
        IsAdvancing = true;

        // Run on-choose commands
        for (int i = 0; i < chosen.Source.OnChooseCommands.Count; i++)
            chosen.Source.OnChooseCommands[i].Execute();

        GoTo(chosen.Source.NextNodeId);
    }

    private void GoTo(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            StopDialogue();
            return;
        }

        var next = _graph.GetNode(nodeId);
        if (next == null)
        {
            Debug.LogWarning($"DialogueGraph '{_graph.name}' missing node '{nodeId}'.");
            StopDialogue();
            return;
        }

        _current = next;
        EnterNode(_current);
    }

    private void EnterNode(DialogueNode node)
    {
        Debug.Log($"[DialogueRunner] Enter {node.NodeType} node: {node.Id}");

        // If we are entering an auto node, we should be advancing.
        if (IsAutoNode(node.NodeType)) IsAdvancing = true;

        switch (node.NodeType)
        {
            case DialogueNodeType.Line:
                {
                    var ln = (LineNode)node;

                    // On-enter commands (optional)
                    for (int i = 0; i < ln.OnEnterCommands.Count; i++)
                        ln.OnEnterCommands[i].Execute();

                    // ✅ We are now waiting for user input (Continue)
                    IsAdvancing = false;
                    OnShowLine?.Invoke(ln.Speaker, ln.Text);
                    break;
                }

            case DialogueNodeType.Choice:
                {
                    var cn = (ChoiceNode)node;
                    var presented = BuildPresentedChoices(cn);

                    // ✅ We are now waiting for user input (Choose)
                    IsAdvancing = false;
                    OnShowChoices?.Invoke(presented);
                    break;
                }

            case DialogueNodeType.Branch:
                {
                    var bn = (BranchNode)node;
                    bool result = bn.Condition == null || bn.Condition.Evaluate();

                    // Still traversing automatically
                    IsAdvancing = true;
                    GoTo(result ? bn.TrueNextNodeId : bn.FalseNextNodeId);
                    break;
                }

            case DialogueNodeType.Command:
                {
                    var cmd = (CommandNode)node;

                    for (int i = 0; i < cmd.Commands.Count; i++)
                        cmd.Commands[i].Execute();

                    // Still traversing automatically
                    IsAdvancing = true;

                    // auto-continue
                    GoTo(cmd.NextNodeId);
                    break;
                }

            case DialogueNodeType.End:
            default:
                StopDialogue();
                break;
        }
    }

    private static List<PresentedChoice> BuildPresentedChoices(ChoiceNode cn)
    {
        var result = new List<PresentedChoice>(cn.Choices.Count);
        for (int i = 0; i < cn.Choices.Count; i++)
        {
            var c = cn.Choices[i];
            bool ok = c.Conditions == null || c.Conditions.Evaluate();
            if (!ok) continue;

            result.Add(new PresentedChoice { Text = c.Text, Source = c });
        }
        return result;
    }
    public string DebugCurrentNodeInfo
    {
        get
        {
            if (_current == null) return "(null)";
            return $"{_current.NodeType}({_current.Id})";
        }
    }

    private static bool IsInputNode(DialogueNodeType t)
        => t == DialogueNodeType.Line || t == DialogueNodeType.Choice;

    private static bool IsAutoNode(DialogueNodeType t)
        => t == DialogueNodeType.Command || t == DialogueNodeType.Branch;

}

public struct PresentedChoice
{
    public string Text;
    public DialogueChoice Source;
}

