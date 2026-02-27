using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueTurnAction
{
    Continue,    // show "Continue"
    PlayerReply, // show player's reply text on the button (confirm to "say" it)
    Choices,     // show list of choices
    Close        // show final "Continue/Close" button at the end
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

public struct DialogueContext
{
    public string NpcId;
    public string LocationId;

    public static DialogueContext From(string npcId, string locationId)
    {
        return new DialogueContext
        {
            NpcId = npcId,
            LocationId = locationId
        };
    }
}

public class DialogueRunner : MonoBehaviour
{
    public static DialogueRunner Instance { get; private set; }

    // If we show Close because peek saw End, we may still need to execute trailing Command/Branch nodes.
    private string _pendingCloseTraversalStartId;

    public event Action<DialogueTurn> OnTurn;
    public event Action OnHideDialogue;
    public event Action<bool> OnDialogueStateChanged;

    private DialogueGraph _graph;
    private DialogueNode _current;

    public bool IsRunning => _graph != null && _current != null;

    /// <summary>True while runner is traversing nodes; UI input should be ignored while true.</summary>
    public bool IsAdvancing { get; private set; }

    // Pending player reply state: UI displays it on the button; runner executes it only on submit
    private bool _waitingForPlayerReply;
    private string _replyTraversalStartId;
    private string _replyLineNodeId;

    // Close step state
    private bool _waitingForClose;

    // Journal run state
    private string _activeDialogueId;
    private bool _hasStartedJournal;

    // You can later replace this with NpcManager.PlayerSpeakerId if you want
    private const string PlayerSpeakerId = "Player";

    //QUESTS related
    public static event Action<DialogueRunner> InstanceReady;
    //public event Action<string, string> OnDialogueFinished; // (npcId, dialogueId)
    private DialogueContext _context;
    private bool _hasContext;
    //quest new flow
    public event Action<string> OnTalkedToNpc; // npcId

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        InstanceReady?.Invoke(this);
        DontDestroyOnLoad(gameObject);
    }

    public void StartDialogue(DialogueGraph graph)
    {
        StartDialogue(graph, default);
    }

    public void StartDialogue(DialogueGraph graph, DialogueContext context)
    {
        if (graph == null) return;

        _context = context;
        _hasContext = !string.IsNullOrEmpty(context.NpcId) || !string.IsNullOrEmpty(context.LocationId);

        _graph = graph;
        _current = _graph.GetNode(_graph.StartNodeId);

        if (_current == null)
        {
            Debug.LogWarning($"DialogueGraph '{graph.name}' has invalid StartNodeId.");
            StopDialogue();
            return;
        }

        // ---- Journal start ----
        _activeDialogueId = !string.IsNullOrEmpty(_graph.DialogueId)? _graph.DialogueId: _graph.name; // fallback so quests/journal still work 
        _hasStartedJournal = false;

        var journal = DialogueJournal.Instance;
        if (journal != null && !string.IsNullOrEmpty(_activeDialogueId))
        {
            journal.OnDialogueStarted(_activeDialogueId);
            _hasStartedJournal = true;
            journal.GetOrCreateProgress(_activeDialogueId)?.MarkNodeVisited(_current.Id);
        }

        OnDialogueStateChanged?.Invoke(true);

        IsAdvancing = true;
        EmitNextTurnFrom(_current.Id);
    }

    private void StopDialogue()
    {
        // ---- Journal finish ----
        if (_graph != null && _hasStartedJournal && !string.IsNullOrEmpty(_activeDialogueId))
        {
            DialogueJournal.Instance?.OnDialogueFinished(
                _activeDialogueId,
                _current != null ? _current.Id : null
            );
        }

        // quest new flow - Notify systems that a talk with NPC happened
        if (_hasContext && !string.IsNullOrEmpty(_context.NpcId))
        {
            OnTalkedToNpc?.Invoke(_context.NpcId);
        }
        _graph = null;
        _current = null;
        _pendingCloseTraversalStartId = null;
        _waitingForPlayerReply = false;
        _replyTraversalStartId = null;
        _replyLineNodeId = null;
        _waitingForClose = false;
        IsAdvancing = false;
        _activeDialogueId = null;
        _hasStartedJournal = false;
        //quest related
        _context = default;
        _hasContext = false;

        OnDialogueStateChanged?.Invoke(false);
        OnHideDialogue?.Invoke();
    }

    public void CloseDialogue()
    {
        FinalizeAndStop();
    }

    public void Continue()
    {
        if (!IsRunning) return;
        if (IsAdvancing) return;

        if (_waitingForClose)
        {
            FinalizeAndStop();
            return;
        }

        if (_waitingForPlayerReply)
        {
            // UI must call SubmitPlayerReply()
            return;
        }

        string nextId = GetNextIdFromCurrent();
        if (string.IsNullOrEmpty(nextId)) { EmitCloseTurn(); return; }

        IsAdvancing = true;
        EmitNextTurnFrom(nextId);
    }

    public void SubmitPlayerReply()
    {
        if (!IsRunning) return;
        if (IsAdvancing) return;
        if (!_waitingForPlayerReply) return;

        IsAdvancing = true;

        var node = TraverseExecuting(_replyTraversalStartId, out var endReason);
        if (endReason == EndReason.End || node == null) { EmitCloseTurn(); return; }

        if (node.NodeType != DialogueNodeType.Line || node.Id != _replyLineNodeId)
        {
            Debug.LogWarning($"[DialogueRunner] SubmitPlayerReply landed on unexpected node '{node.NodeType}({node.Id})'. Expected Line({_replyLineNodeId}).");
            EmitCloseTurn();
            return;
        }

        var ln = (LineNode)node;

        // Journal: player confirmed this reply (now it's "seen/done")
        if (!string.IsNullOrEmpty(_activeDialogueId))
            DialogueJournal.Instance?.MarkLineSeen(BuildLineId(_activeDialogueId, ln.Id));

        // Execute on-enter commands at the moment player confirms the reply
        for (int i = 0; i < ln.OnEnterCommands.Count; i++)
            ln.OnEnterCommands[i].Execute();

        _waitingForPlayerReply = false;

        if (string.IsNullOrEmpty(ln.NextNodeId)) { EmitCloseTurn(); return; }

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

        // Journal: choice was selected
        if (!string.IsNullOrEmpty(_activeDialogueId))
            DialogueJournal.Instance?.MarkChoiceSeen(BuildChoiceId(_activeDialogueId, choiceNode.Id, chosen.SourceIndex));

        IsAdvancing = true;

        for (int i = 0; i < chosen.Source.OnChooseCommands.Count; i++)
            chosen.Source.OnChooseCommands[i].Execute();

        if (string.IsNullOrEmpty(chosen.Source.NextNodeId)) { EmitCloseTurn(); return; }

        EmitNextTurnFrom(chosen.Source.NextNodeId);
    }

    // -----------------------------
    // Turn emission
    // -----------------------------

    private enum EndReason { None, End }

    private void EmitNextTurnFrom(string startNodeId)
    {
        if (string.IsNullOrEmpty(startNodeId))
        {
            EmitCloseTurn();
            return;
        }

        // Reset per-turn input states unless we set them again this turn
        _waitingForPlayerReply = false;
        _replyTraversalStartId = null;
        _replyLineNodeId = null;
        _waitingForClose = false;
        _pendingCloseTraversalStartId = null;

        var first = TraverseExecuting(startNodeId, out var endReason);
        if (endReason == EndReason.End || first == null)
        {
            EmitCloseTurn();
            return;
        }

        _current = first;

        // Journal: node visited
        if (!string.IsNullOrEmpty(_activeDialogueId))
            DialogueJournal.Instance?.GetOrCreateProgress(_activeDialogueId)?.MarkNodeVisited(first.Id);

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

                // Journal: NPC line was shown to player
                if (!string.IsNullOrEmpty(_activeDialogueId))
                    DialogueJournal.Instance?.MarkLineSeen(BuildLineId(_activeDialogueId, ln.Id));

                // Decide next required action (peek only; does not execute)
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
                    turn.Action = DialogueTurnAction.Close;
                    _waitingForClose = true;
                    _pendingCloseTraversalStartId = ln.NextNodeId;
                }
                else
                {
                    // next is NPC line or choices -> user must "Continue"
                    turn.Action = DialogueTurnAction.Continue;
                }
            }
            else
            {
                // Player line encountered directly: show reply button, execute on submit
                // (Do NOT mark line seen here; mark on SubmitPlayerReply)
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
            // Anything unexpected -> close gracefully
            turn.Action = DialogueTurnAction.Close;
            _waitingForClose = true;
        }

        IsAdvancing = false;
        OnTurn?.Invoke(turn);
    }

    private void EmitCloseTurn()
    {
        if (!IsRunning)
        {
            // If already stopped, don't emit anything
            //StopDialogue();
            return;
        }

        _waitingForPlayerReply = false;
        _replyTraversalStartId = null;
        _replyLineNodeId = null;
        _pendingCloseTraversalStartId = null;
        _waitingForClose = true;
        IsAdvancing = false;

        OnTurn?.Invoke(new DialogueTurn
        {
            HasNpcLine = false,
            Action = DialogueTurnAction.Close
        });
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

            result.Add(new PresentedChoice
            {
                Text = c.Text,
                Source = c,
                SourceIndex = i
            });
        }
        return result;
    }

    private bool IsPlayerSpeakerId(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker)) return false;
        return string.Equals(speaker.Trim(), PlayerSpeakerId, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------
    // Journal ID helpers
    // -----------------------------

    private static string BuildLineId(string dialogueId, string nodeId)
    {
        // Stable as long as nodeId is stable.
        return $"{dialogueId}:line:{nodeId}";
    }

    private static string BuildChoiceId(string dialogueId, string choiceNodeId, int optionIndex)
    {
        // Stable as long as option ordering is stable (upgrade later to ChoiceOptionId if needed).
        return $"{dialogueId}:choice:{choiceNodeId}:{optionIndex}";
    }
    private void ExecutePendingCloseTraversalIfAny()
    {
        if (!IsRunning) return;
        if (IsAdvancing) return;

        if (string.IsNullOrEmpty(_pendingCloseTraversalStartId))
            return;

        IsAdvancing = true;

        TraverseExecuting(_pendingCloseTraversalStartId, out _);

        _pendingCloseTraversalStartId = null;

        IsAdvancing = false;
    }
    private void FinalizeAndStop()
    {
        // Always execute any trailing command/branch chain that was deferred by "peek -> Close"
        ExecutePendingCloseTraversalIfAny();

        // Then do the real shutdown + journal finish + UI hide
        StopDialogue();
    }

}

public struct PresentedChoice
{
    public string Text;
    public DialogueChoice Source;

    // Index of Source in the ChoiceNode's Choices list (for stable-ish choiceId composition)
    public int SourceIndex;
}
