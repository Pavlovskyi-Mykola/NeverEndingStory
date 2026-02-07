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

    public bool IsRunning => _graph != null && _current != null;

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
            StopDialogue();
            return;
        }

        EnterNode(_current);
    }

    public void StopDialogue()
    {
        _graph = null;
        _current = null;
        OnHideDialogue?.Invoke();
    }

    public void Continue()
    {
        if (!IsRunning) return;

        if (_current is LineNode line)
        {
            GoTo(line.NextNodeId);
            return;
        }

        if (_current is CommandNode cmd)
        {
            GoTo(cmd.NextNodeId);
            return;
        }

        // Choice nodes require Choose(index)
        // End nodes will auto-stop
    }

    public void Choose(int presentedChoiceIndex)
    {
        if (!IsRunning) return;
        if (!(_current is ChoiceNode choiceNode)) return;

        var presented = BuildPresentedChoices(choiceNode);
        if (presentedChoiceIndex < 0 || presentedChoiceIndex >= presented.Count) return;

        var chosen = presented[presentedChoiceIndex];

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
        switch (node.NodeType)
        {
            case DialogueNodeType.Line:
                {
                    var ln = (LineNode)node;

                    // On-enter commands (optional)
                    for (int i = 0; i < ln.OnEnterCommands.Count; i++)
                        ln.OnEnterCommands[i].Execute();

                    OnShowLine?.Invoke(ln.Speaker, ln.Text);
                    break;
                }

            case DialogueNodeType.Choice:
                {
                    var cn = (ChoiceNode)node;
                    var presented = BuildPresentedChoices(cn);
                    OnShowChoices?.Invoke(presented);
                    break;
                }

            case DialogueNodeType.Branch:
                {
                    var bn = (BranchNode)node;
                    bool result = bn.Condition == null || bn.Condition.Evaluate();
                    GoTo(result ? bn.TrueNextNodeId : bn.FalseNextNodeId);
                    break;
                }

            case DialogueNodeType.Command:
                {
                    var cmd = (CommandNode)node;
                    for (int i = 0; i < cmd.Commands.Count; i++)
                        cmd.Commands[i].Execute();

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
}

public struct PresentedChoice
{
    public string Text;
    public DialogueChoice Source;
}
