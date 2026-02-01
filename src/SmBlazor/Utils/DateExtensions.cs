namespace SmBlazor.Utils;

public static class DateExtensions
{
    public static DateTime JustDate(this DateTime dt) => dt.Date;

    public static bool NonWorkingDay(this DateTime dt)
        => dt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static DateTime GetPreviousMonday(this DateTime dt)
    {
        // Monday = 1 ... Sunday = 0 in JS's getDay() logic; in .NET Monday=1 Sunday=0 too.
        // We want the Monday of the week containing dt.
        var date = dt.Date;
        int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff);
    }

    public static DateTime GetNextWorkDay(this DateTime dt)
    {
        var date = dt.Date.AddDays(1);
        while (date.NonWorkingDay()) date = date.AddDays(1);
        return date;
    }

    public static DateTime GetPreviousWorkDay(this DateTime dt)
    {
        var date = dt.Date.AddDays(-1);
        while (date.NonWorkingDay()) date = date.AddDays(-1);
        return date;
    }

    /// <summary>
    /// Inclusive start/end range (same semantics as your JS: end.addDays(1) > date).
    /// </summary>
    public static bool IsBetweenInclusive(this DateTime dt, DateTime start, DateTime end)
    {
        var date = dt.Date;
        return start.Date <= date && end.Date.AddDays(1) > date;
    }

    public static bool IsSameDate(this DateTime a, DateTime b) => a.Date == b.Date;
}

