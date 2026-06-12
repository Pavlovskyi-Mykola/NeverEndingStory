public enum StatType
{
    Money,
    Influence,
    Strategy,
    Networking,
    Reputation
}

public static class StatTypes
{
    /// <summary>Cached values — Enum.GetValues allocates a boxed array on every call.</summary>
    public static readonly StatType[] All = (StatType[])System.Enum.GetValues(typeof(StatType));
}
