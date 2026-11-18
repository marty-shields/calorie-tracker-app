using System.Globalization;

namespace CalorieTracker.UI.Navigation;

/// <summary>The span Trends shows.</summary>
public enum TrendsPeriod
{
    Week,
    Month,
    Year,
}

/// <summary>Trends' route and its query: <c>/trends?period=week|month|year&amp;from=yyyy-MM-dd</c>. Without a query it opens on the current week.</summary>
public static class TrendsRoute
{
    public const string Template = "/trends";

    /// <summary>The route that opens <paramref name="period"/> containing <paramref name="from"/>.</summary>
    public static string Url(TrendsPeriod period, DateOnly from) =>
        $"{Template}?period={Name(period)}&from={RouteDate.ToText(from)}";

    /// <summary>The period named by a query value; Week when it is missing or unknown.</summary>
    public static TrendsPeriod ParsePeriod(string? text) => text?.ToLowerInvariant() switch
    {
        "month" => TrendsPeriod.Month,
        "year" => TrendsPeriod.Year,
        _ => TrendsPeriod.Week,
    };

    /// <summary>First day of the period containing <paramref name="day"/>: the Monday of its week, the 1st of its month or 1 January.</summary>
    public static DateOnly Start(TrendsPeriod period, DateOnly day) => period switch
    {
        TrendsPeriod.Month => new DateOnly(day.Year, day.Month, 1),
        TrendsPeriod.Year => new DateOnly(day.Year, 1, 1),
        _ => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
    };

    /// <summary>Last day of the period that starts at <paramref name="start"/>.</summary>
    public static DateOnly End(TrendsPeriod period, DateOnly start) => period switch
    {
        TrendsPeriod.Month => start.AddMonths(1).AddDays(-1),
        TrendsPeriod.Year => new DateOnly(start.Year, 12, 31),
        _ => start.AddDays(6),
    };

    /// <summary>True when the period that starts at <paramref name="start"/> holds <paramref name="today"/> or ends after it: Next stops there.</summary>
    public static bool IsCurrent(TrendsPeriod period, DateOnly start, DateOnly today) => End(period, start) >= today;

    /// <summary>The period start one period before or after <paramref name="start"/>.</summary>
    public static DateOnly Step(TrendsPeriod period, DateOnly start, int direction) => period switch
    {
        TrendsPeriod.Month => start.AddMonths(direction),
        TrendsPeriod.Year => start.AddYears(direction),
        _ => start.AddDays(7 * direction),
    };

    /// <summary>The label of the period strip: "9 – 15 Nov", "30 Nov – 6 Dec" across months, "30 Dec 2026 – 5 Jan 2027" across years, "November 2026" or "2026".</summary>
    public static string Label(TrendsPeriod period, DateOnly start)
    {
        var culture = CultureInfo.InvariantCulture;
        switch (period)
        {
            case TrendsPeriod.Month:
                return start.ToString("MMMM yyyy", culture);
            case TrendsPeriod.Year:
                return start.ToString("yyyy", culture);
        }

        var end = End(period, start);
        if (start.Year != end.Year)
        {
            return $"{start.ToString("d MMM yyyy", culture)} – {end.ToString("d MMM yyyy", culture)}";
        }

        return start.Month == end.Month
            ? $"{start.Day} – {end.ToString("d MMM", culture)}"
            : $"{start.ToString("d MMM", culture)} – {end.ToString("d MMM", culture)}";
    }

    private static string Name(TrendsPeriod period) => period.ToString().ToLowerInvariant();
}
