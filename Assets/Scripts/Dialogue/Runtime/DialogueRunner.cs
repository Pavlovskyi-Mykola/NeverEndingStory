using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueTurnAction
{
    Continue,
    PlayerReply,
    Choices,
    End
}

public struct DialogueTurn
{
    public bool HasNpcLine;
    public string NpcSpeaker;
    public string NpcText;

    public DialogueTurnAction Action;

    // PlayerReply
    public string PlayerSpeaker;
    public string PlayerText;

    // Choices
    public List<PresentedChoice> Choices;
}

public class DialogueRunner : MonoBehaviour
{
    public static DialogueRunner Instance { get; private set; }

    public event Action<DialogueTurn> OnTurn;
    public event Action OnHideDialogue;
    public event Action<bool> OnDialogueStateChanged;

    private DialogueGraph _graph;
    private DialogueNode _current;

    public bool IsRunning => _graph != null && _current != null;

    /// <summary>True while runner is traversing nodes; UI input should be ignored while true.</summary>
    public bool IsAdvancing { get; private set; }

    // Pending reply state (UI shows it on the button; runner executes it only on submit)
    private bool _waitingForPlayerReply;
    private string _replyTraversalStartId;
    private string _replyLineNodeId;

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

        OnDialogueStateChanged?.Invoke(true);

        IsAdvancing = true;
        EmitNextTurnFrom(_current.Id);
    }

    public void StopDialogue()
    {
        _graph = null;
        _current = null;

        _waitingForPlayerReply = false;
        _replyTraversalStartId = null;
        _replyLineNodeId = null;

        IsAdvancing = false;

        OnDialogueStateChanged?.Invoke(false);
        OnHideDialogue?.Invoke();
    }

    public void Continue()
    {
        if (!IsRunning) return;
        if (IsAdvancing) return;
        if (_waitingForPlayerReply) return; // must submit reply instead

        string nextId = GetNextIdFromCurrent();
        if (string.IsNullOrEmpty(nextId)) { StopDialogue(); return; }

        IsAdvancing = true;
        EmitNextTurnFrom(nextId);
    }

    public void SubmitPlayerReply()
    {
        if (!IsRunning) return;
        if (IsAdvancing) return;
        if (!_waitingForPlayerReply) return;

        IsAdvancing = true;

        // Traverse executing auto nodes until we hit the expected player line
        var node = TraverseExecuting(_replyTraversalStartId, out var endReason);
        if (endReason == EndReason.End || node == null) { StopDialogue(); return; }

        if (node.NodeType != DialogueNodeType.Line || node.Id != _replyLineNodeId)
        {
            Debug.LogWarning($"[DialogueRunner] SubmitPlayerReply landed on unexpected node '{node.NodeType}({node.Id})'. Expected Line({_replyLineNodeId}).");
            StopDialogue();
            return;
        }

        var ln = (LineNode)node;

        // Execute on-enter commands at the moment player confirms the reply
        for (int i = 0; i < ln.OnEnterCommands.Count; i++)
            ln.OnEnterCommands[i].Execute();

        _waitingForPlayerReply = false;

        if (string.IsNullOrEmpty(ln.NextNodeId)) { StopDialogue(); return; }

        EmitNextTurnFrom(ln.NextNodeId);
    }

    public void Choose(int presentedChoiceIndex)
    {
        if (!IsRunning) return;
        if (IsAdvancing) return;
        if (!(_current is ChoiceNode choiceNode)) return;

        var presented = BuildPresentedChoices(choiceNode);
        if (presentedChoiceIndex < 0 || presentedChoiceIndex >= presented.Count) return;

        var chosen = presented[presentedChoiceIndex];

        IsAdvancing = true;

        for (int i = 0; i < chosen.Source.OnChooseCommands.Count; i++)
            chosen.Source.OnChooseCommands[i].Execute();

        if (string.IsNullOrEmpty(chosen.Source.NextNodeId)) { StopDialogue(); return; }

        EmitNextTurnFrom(chosen.Source.NextNodeId);
    }

    // -----------------------------
    // Turn emission
    // -----------------------------

    private enum EndReason { None, End }

    private void EmitNextTurnFrom(string startNodeId)
    {
        if (string.IsNullOrEmpty(startNodeId)) { StopDialogue(); return; }

        // Reset pending reply unless we set it again this turn
        _waitingForPlayerReply = false;
        _replyTraversalStartId = null;
        _replyLineNodeId = null;

        // Traverse executing auto nodes until we reach Line/Choice/End
        var first = TraverseExecuting(startNodeId, out var endReason);
        if (endReason == EndReason.End || first == null) { StopDialogue(); return; }

        _current = first;

        var turn = new DialogueTurn();

        if (first.NodeType == DialogueNodeType.Line)
        {
            var ln = (LineNode)first;
            bool isPlayer = IsPlayerSpeakerId(ln.Speaker);

            if (!isPlayer)
            {
                // NPC line: execute on-enter now and show it
                for (int i = 0; i < ln.OnEnterCommands.Count; i++)
                    ln.OnEnterCommands[i].Execute();

                turn.HasNpcLine = true;
                turn.NpcSpeaker = ln.Speaker;
                turn.NpcText = ln.Text;

                // Decide next required action
                var next = PeekNextInput(ln.NextNodeId);

                if (next.Kind == NextPeekKind.PlayerLine)
                {
                    turn.Action = DialogueTurnAction.PlayerReply;
                    turn.PlayerSpeaker = next.Speaker;
                    turn.PlayerText = next.Text;

                    _waitingForPlayerReply = true;
                    _replyTraversalStartId = ln.NextNodeId;
                    _replyLineNodeId = next.NodeId;
                }
                else if (next.Kind == NextPeekKind.End)
                {
                    turn.Action = DialogueTurnAction.End;
                }
                else
                {
                    turn.Action = DialogueTurnAction.Continue;
                }
            }
            else
            {
                // Player line encountered directly: show as reply button, execute on submit
                turn.HasNpcLine = false;
                turn.Action = DialogueTurnAction.PlayerReply;
                turn.PlayerSpeaker = ln.Speaker;
                turn.PlayerText = ln.Text;

                _waitingForPlayerReply = true;
                _replyTraversalStartId = ln.Id;
                _replyLineNodeId = ln.Id;
            }
        }
        else if (first.NodeType == DialogueNodeType.Choice)
        {
            var cn = (ChoiceNode)first;
            turn.HasNpcLine = false;
            turn.Action = DialogueTurnAction.Choices;
            turn.Choices = BuildPresentedChoices(cn);
        }
        else
        {
            turn.Action = DialogueTurnAction.End;
        }

        IsAdvancing = false;
        OnTurn?.Invoke(turn);

        if (turn.Action == DialogueTurnAction.End)
            StopDialogue();
    }

    private string GetNextIdFromCurrent()
    {
        if (_current == null) return null;

        return _current.NodeType switch
        {
            DialogueNodeType.Line => ((LineNode)_current).NextNodeId,
            DialogueNodeType.Command => ((CommandNode)_current).NextNodeId,
            _ => null
        };
    }

    // -----------------------------
    // Traversal (executing)
    // -----------------------------

    private DialogueNode TraverseExecuting(string startId, out EndReason endReason)
    {
        endReason = EndReason.None;

        var safety = 0;
        var node = _graph.GetNode(startId);

        while (node != null && safety++ < 256)
        {
            switch (node.NodeType)
            {
                case DialogueNodeType.Command:
                    {
                        var cmd = (CommandNode)node;
                        for (int i = 0; i < cmd.Commands.Count; i++)
                            cmd.Commands[i].Execute();

                        if (string.IsNullOrEmpty(cmd.NextNodeId))
                        {
                            endReason = EndReason.End;
                            return null;
                        }

                        node = _graph.GetNode(cmd.NextNodeId);
                        break;
                    }

                case DialogueNodeType.Branch:
                    {
                        var bn = (BranchNode)node;
                        bool result = bn.Condition == null || bn.Condition.Evaluate();
                        var next = result ? bn.TrueNextNodeId : bn.FalseNextNodeId;

                        if (string.IsNullOrEmpty(next))
                        {
                            endReason = EndReason.End;
                            return null;
                        }

                        node = _graph.GetNode(next);
                        break;
                    }

                case DialogueNodeType.End:
                    endReason = EndReason.End;
                    return null;

                case DialogueNodeType.Line:
                case DialogueNodeType.Choice:
                    return node;

                default:
                    Debug.LogWarning($"[DialogueRunner] Unknown node type '{node.NodeType}'.");
                    endReason = EndReason.End;
                    return null;
            }
        }

        endReason = EndReason.End;
        return null;
    }

    // -----------------------------
    // Peek (non-executing)
    // -----------------------------

    private enum NextPeekKind { None, NpcLine, PlayerLine, Choice, End }

    private struct NextPeekInfo
    {
        public NextPeekKind Kind;
        public string NodeId;
        public string Speaker;
        public string Text;
    }

    private NextPeekInfo PeekNextInput(string startId)
    {
        if (string.IsNullOrEmpty(startId))
            return new NextPeekInfo { Kind = NextPeekKind.End };

        var safety = 0;
        var node = _graph.GetNode(startId);

        while (node != null && safety++ < 256)
        {
            switch (node.NodeType)
            {
                case DialogueNodeType.Line:
                    {
                        var ln = (LineNode)node;
                        bool isPlayer = IsPlayerSpeakerId(ln.Speaker);
                        return new NextPeekInfo
                        {
                            Kind = isPlayer ? NextPeekKind.PlayerLine : NextPeekKind.NpcLine,
                            NodeId = ln.Id,
                            Speaker = ln.Speaker,
                            Text = ln.Text
                        };
                    }

                case DialogueNodeType.Choice:
                    return new NextPeekInfo { Kind = NextPeekKind.Choice };

                case DialogueNodeType.End:
                    return new NextPeekInfo { Kind = NextPeekKind.End };

                case DialogueNodeType.Command:
                    {
                        var cmd = (CommandNode)node;
                        if (string.IsNullOrEmpty(cmd.NextNodeId))
                            return new NextPeekInfo { Kind = NextPeekKind.End };
                        node = _graph.GetNode(cmd.NextNodeId);
                        break;
                    }

                case DialogueNodeType.Branch:
                    {
                        var bn = (BranchNode)node;
                        bool result = bn.Condition == null || bn.Condition.Evaluate();
                        var next = result ? bn.TrueNextNodeId : bn.FalseNextNodeId;

                        if (string.IsNullOrEmpty(next))
                            return new NextPeekInfo { Kind = NextPeekKind.End };

                        node = _graph.GetNode(next);
                        break;
                    }

                default:
                    return new NextPeekInfo { Kind = NextPeekKind.None };
            }
        }

        return new NextPeekInfo { Kind = NextPeekKind.None };
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

    private bool IsPlayerSpeakerId(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker)) return false;
        return string.Equals(speaker.Trim(), "Player", StringComparison.OrdinalIgnoreCase);
    }
}

public struct PresentedChoice
{
    public string Text;
    public DialogueChoice Source;
}
