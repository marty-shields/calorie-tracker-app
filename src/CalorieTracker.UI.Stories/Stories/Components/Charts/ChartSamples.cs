using CalorieTracker.UI.Components.Charts;

namespace CalorieTracker.UI.Stories.Stories.Components.Charts;

internal static class ChartSamples
{
    public const double Target = 2100;

    public static readonly DateOnly Today = new(2026, 11, 18);

    private static readonly string[] Weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly DateOnly Monday = new(2026, 11, 9);

    public static IReadOnlyList<TrendBar> Week(params double?[] values) =>
        values.Select((v, i) => new TrendBar(Monday.AddDays(i), Weekdays[i], v)).ToList();

    /// <summary>Under, on, on, over, not logged, on, under.</summary>
    public static IReadOnlyList<TrendBar> MixedWeek { get; } = Week(1650, 2050, 2180, 2390, null, 1900, 1200);

    public static IReadOnlyList<TrendBar> AllOverWeek { get; } = Week(2500, 2780, 3120, 2450, 2900, 3300, 2600);

    public static IReadOnlyList<TrendBar> EmptyWeek { get; } = Week(null, null, null, null, null, null, null);

    public static IReadOnlyList<TrendBar> SingleBar { get; } = Week(null, null, 2050, null, null, null, null);

    public static IReadOnlyList<TrendBar> LongLabels { get; } = Weekdays
        .Select((day, i) => new TrendBar(Monday.AddDays(i), $"{day}day afternoon", 1800 + i * 90))
        .ToList();

    public static IReadOnlyList<TrendBar> Year { get; } = Months
        .Select((m, i) => new TrendBar(new DateOnly(2026, i + 1, 1), m, i is 3 or 11 ? null : 1700 + (i * 137 % 700)))
        .ToList();

    /// <summary>A month's days: one in three is under, then on, then over; every fifth is not logged. Only days before <see cref="Today"/> carry data.</summary>
    public static IReadOnlyList<CalendarDay> Month(int year, int month, DateOnly through) =>
        Enumerable.Range(1, DateTime.DaysInMonth(year, month))
            .Select(d => new DateOnly(year, month, d))
            .Where(d => d <= through)
            .Select(d => new CalendarDay(d, d.Day % 5 == 0 ? null : (d.Day % 3) switch { 0 => 1500, 1 => 2100, _ => 2600 }))
            .ToList();

    public static IReadOnlyList<CalendarDay> AllOver(int year, int month, DateOnly through) =>
        Month(year, month, through).Select(d => d with { Value = d.Value is null ? null : 2800 }).ToList();
}
