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

    /// <summary>NpcId of the current conversation partner, or null outside dialogue.
    /// Lets commands/conditions target "the person I'm talking to" implicitly.</summary>
    public string CurrentNpcId => _hasContext && IsRunning ? _context.NpcId : null;

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

    // Choices shown to the UI for the current turn; Choose() must index into this
    // exact list, not a rebuilt one (conditions may have changed since emission).
    private List<PresentedChoice> _presentedChoices;

    //QUESTS related
    public static event Action<DialogueRunner> InstanceReady;
    private DialogueContext _context;
    private bool _hasContext;

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

        if (IsRunning)
        {
            Debug.LogWarning($"[DialogueRunner] StartDialogue('{graph.name}') ignored — dialogue '{_activeDialogueId}' is still running.");
            return;
        }

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

        // Cache before we clear state
        var finishedDialogueId = _activeDialogueId;
        var npcId = _hasContext ? _context.NpcId : null;

        ClearRunState();

        // Notify systems that a talk with the NPC happened (after state is cleared,
        // so listeners that start new dialogues/actions see a non-running runner).
        if (!string.IsNullOrEmpty(npcId))
            GameEvents.RaiseNpcTalked(npcId, finishedDialogueId);

        OnDialogueStateChanged?.Invoke(false);
        OnHideDialogue?.Invoke();
    }

    /// <summary>
    /// Hard-stops a running dialogue without executing trailing commands, finishing
    /// the journal, or raising NpcTalked. For save/load and new game, where those
    /// side effects would corrupt the freshly restored state.
    /// </summary>
    public void AbortDialogue()
    {
        if (!IsRunning) return;

        ClearRunState();

        OnDialogueStateChanged?.Invoke(false);
        OnHideDialogue?.Invoke();
    }

    private void ClearRunState()
    {
        _graph = null;
        _current = null;
        _pendingCloseTraversalStartId = null;
        _waitingForPlayerReply = false;
        _replyTraversalStartId = null;
        _replyLineNodeId = null;
        _waitingForClose = false;
        _presentedChoices = null;
        IsAdvancing = false;
        _activeDialogueId = null;
        _hasStartedJournal = false;
        _context = default;
        _hasContext = false;
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

        // Index into the exact list the UI was built from. Rebuilding here could
        // re-evaluate conditions and silently map the index to a different choice.
        var presented = _presentedChoices;
        if (presented == null) return;
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
        _presentedChoices = null;

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
            _presentedChoices = BuildPresentedChoices(cn);
            turn.Choices = _presentedChoices;
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
        // If already stopped, don't emit anything
        if (!IsRunning)
            return;

        _waitingForPlayerReply = false;
        _replyTraversalStartId = null;
        _replyLineNodeId = null;
        _pendingCloseTraversalStartId = null;
        _presentedChoices = null;
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
    // Traversal (shared by executing advance and non-executing peek)
    // -----------------------------

    /// <summary>
    /// Walks Command/Branch/End nodes from startId until reaching a Line or Choice
    /// node (returned) or an end (null + EndReason.End). When execute is true,
    /// Command nodes run their commands; Branch conditions are evaluated either way.
    /// </summary>
    private DialogueNode Traverse(string startId, bool execute, out EndReason endReason)
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

                        if (execute)
                        {
                            for (int i = 0; i < cmd.Commands.Count; i++)
                                cmd.Commands[i].Execute();
                        }

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

    private DialogueNode TraverseExecuting(string startId, out EndReason endReason) =>
        Traverse(startId, execute: true, out endReason);

    // -----------------------------
    // Peek (non-executing)
    // -----------------------------

    private enum NextPeekKind { NpcLine, PlayerLine, Choice, End }

    private struct NextPeekInfo
    {
        public NextPeekKind Kind;
        public string NodeId;
        public string Speaker;
        public string Text;
    }

    private NextPeekInfo PeekNextInput(string startId)
    {
        var node = Traverse(startId, execute: false, out _);

        if (node == null)
            return new NextPeekInfo { Kind = NextPeekKind.End };

        if (node.NodeType == DialogueNodeType.Choice)
            return new NextPeekInfo { Kind = NextPeekKind.Choice };

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
        return string.Equals(speaker.Trim(), NpcManager.PlayerSpeakerId, StringComparison.OrdinalIgnoreCase);
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
