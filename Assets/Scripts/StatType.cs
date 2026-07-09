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

    /// <summary>Player-facing delta text: "+$50" / "-$50" for money, "+2 Influence" otherwise.</summary>
    public static string FormatDelta(StatType stat, int delta)
    {
        if (stat == StatType.Money)
            return delta > 0 ? $"+${delta}" : $"-${-delta}";

        return delta > 0 ? $"+{delta} {stat}" : $"-{-delta} {stat}";
    }
}
