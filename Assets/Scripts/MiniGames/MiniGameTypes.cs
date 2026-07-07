public enum MiniGameTier
{
    Failed = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3
}

public readonly struct MiniGameResult
{
    public readonly MiniGameTier Tier;

    public bool Success => Tier != MiniGameTier.Failed;

    public MiniGameResult(MiniGameTier tier)
    {
        Tier = tier;
    }
}

/// <summary>What a mini-game knows about why it was launched.</summary>
public readonly struct MiniGameContext
{
    public readonly MiniGameDefinition Definition;
    public readonly ActionDefinition SourceAction;

    public MiniGameContext(MiniGameDefinition definition, ActionDefinition sourceAction)
    {
        Definition = definition;
        SourceAction = sourceAction;
    }
}
