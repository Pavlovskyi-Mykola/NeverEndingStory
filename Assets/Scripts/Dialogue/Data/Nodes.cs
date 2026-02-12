using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueNodeType
{
    Line,
    Choice,
    Branch,
    Command,
    End
}

[Serializable]
public abstract class DialogueNode
{
    [SerializeField] private string id;
    [SerializeField] private Vector2 editorPosition; // used later in custom editor

    public string Id => id;
    public Vector2 EditorPosition => editorPosition;

    public abstract DialogueNodeType NodeType { get; }

    protected DialogueNode(string id)
    {
        this.id = id;
        editorPosition = Vector2.zero;
    }
}

[Serializable]
public class LineNode : DialogueNode
{
    [SerializeField] private string speaker;
    [TextArea(3, 8)]
    [SerializeField] private string text;
    [SerializeField] private string nextNodeId;

    [SerializeField] private List<DialogueCommand> onEnterCommands = new();

    public override DialogueNodeType NodeType => DialogueNodeType.Line;

    public string Speaker => speaker;
    public string Text => text;
    public string NextNodeId => nextNodeId;
    public IReadOnlyList<DialogueCommand> OnEnterCommands => onEnterCommands;

    public LineNode(string id) : base(id) { }
}

[Serializable]
public class ChoiceNode : DialogueNode
{
    [SerializeField] private List<DialogueChoice> choices = new();

    public override DialogueNodeType NodeType => DialogueNodeType.Choice;
    public IReadOnlyList<DialogueChoice> Choices => choices;

    public ChoiceNode(string id) : base(id) { }
}

[Serializable]
public class BranchNode : DialogueNode
{
    [SerializeField] private DialogueConditionGroup condition = new();
    [SerializeField] private string trueNextNodeId;
    [SerializeField] private string falseNextNodeId;

    public override DialogueNodeType NodeType => DialogueNodeType.Branch;

    public DialogueConditionGroup Condition => condition;
    public string TrueNextNodeId => trueNextNodeId;
    public string FalseNextNodeId => falseNextNodeId;

    public BranchNode(string id) : base(id) { }
}

[Serializable]
public class CommandNode : DialogueNode
{
    [SerializeField] private List<DialogueCommand> commands = new();
    [SerializeField] private string nextNodeId;

    public override DialogueNodeType NodeType => DialogueNodeType.Command;

    public IReadOnlyList<DialogueCommand> Commands => commands;
    public string NextNodeId => nextNodeId;

    public CommandNode(string id) : base(id) { }
}

[Serializable]
public class EndNode : DialogueNode
{
    public override DialogueNodeType NodeType => DialogueNodeType.End;
    public EndNode(string id) : base(id) { }
}

[Serializable]
public class DialogueChoice
{
    [SerializeField] private string text;
    [SerializeField] private string nextNodeId;

    [SerializeField] private DialogueConditionGroup conditions = new();
    [SerializeField] private List<DialogueCommand> onChooseCommands = new();

    public string Text => text;
    public string NextNodeId => nextNodeId;
    public DialogueConditionGroup Conditions => conditions;
    public IReadOnlyList<DialogueCommand> OnChooseCommands => onChooseCommands;
}
