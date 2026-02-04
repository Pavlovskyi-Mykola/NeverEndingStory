using System;

[Flags]
public enum DayPhaseMask
{
    None = 0,
    Morning = 1 << 0,
    Afternoon = 1 << 1,
    Evening = 1 << 2,
    Night = 1 << 3,
    All = Morning | Afternoon | Evening | Night
}

public static class DayPhaseMaskExtensions
{
    public static DayPhaseMask From(DayPhase phase)
    {
        return phase switch
        {
            DayPhase.Morning => DayPhaseMask.Morning,
            DayPhase.Afternoon => DayPhaseMask.Afternoon,
            DayPhase.Evening => DayPhaseMask.Evening,
            DayPhase.Night => DayPhaseMask.Night,
            _ => DayPhaseMask.None
        };
    }
}

[Flags]
public enum DayOfWeekMask
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,
    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday
}

public static class DayOfWeekMaskExtensions
{
    public static DayOfWeekMask From(DayOfWeek day)
    {
        // System.DayOfWeek: Sunday=0 ... Saturday=6
        return day switch
        {
            DayOfWeek.Sunday => DayOfWeekMask.Sunday,
            DayOfWeek.Monday => DayOfWeekMask.Monday,
            DayOfWeek.Tuesday => DayOfWeekMask.Tuesday,
            DayOfWeek.Wednesday => DayOfWeekMask.Wednesday,
            DayOfWeek.Thursday => DayOfWeekMask.Thursday,
            DayOfWeek.Friday => DayOfWeekMask.Friday,
            DayOfWeek.Saturday => DayOfWeekMask.Saturday,
            _ => DayOfWeekMask.None
        };
    }
}
