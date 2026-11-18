namespace CalorieTracker.UI.Components.Charts;

/// <summary>How a day's calories compare with its target. None means there was no target to compare with.</summary>
public enum TargetStatus { None, Under, On, Over }

public static class TargetStatuses
{
    /// <summary>A day is on target within this share either side of its target.</summary>
    public const double Tolerance = 0.1;

    public static TargetStatus Of(double value, double? target)
    {
        if (target is not > 0)
        {
            return TargetStatus.None;
        }

        return value < target.Value * (1 - Tolerance) - 1e-9 ? TargetStatus.Under
            : value > target.Value * (1 + Tolerance) + 1e-9 ? TargetStatus.Over
            : TargetStatus.On;
    }

    public static string Word(TargetStatus status) => status switch
    {
        TargetStatus.Under => "under target",
        TargetStatus.On => "on target",
        TargetStatus.Over => "over target",
        _ => "",
    };
}

/// <summary>One bar of a <see cref="TrendBarChart"/>.</summary>
/// <param name="Date">What <c>OnSelect</c> returns. For a month bar, any day of that month.</param>
/// <param name="Label">The short text under the bar, such as "Mon".</param>
/// <param name="Value">Calories. Null, or zero or less, means nothing was logged and leaves an empty slot.</param>
/// <param name="Target">The target this bar is judged against, when it differs from the chart's.</param>
public sealed record TrendBar(DateOnly Date, string Label, double? Value, double? Target = null);

/// <summary>One logged (or not logged) day of a <see cref="MonthCalendar"/>.</summary>
/// <param name="Date">Matched against each day of the month; a date outside the month is ignored.</param>
/// <param name="Value">Calories. Null, or zero or less, means nothing was logged.</param>
/// <param name="Target">The target that day had, when it differs from the calendar's.</param>
public sealed record CalendarDay(DateOnly Date, double? Value, double? Target = null);

/// <summary>One row of a <see cref="BarList"/>.</summary>
/// <param name="Label">The row's name.</param>
/// <param name="Value">Sets the bar's length against the list's largest value.</param>
/// <param name="ValueText">What to show at the end of the row, when not the plain number.</param>
public sealed record BarListItem(string Label, double Value, string? ValueText = null);

public enum LegendKind { OnTarget, Over, Under, NotLogged, TargetLine, TargetBand }

/// <summary>One entry of a <see cref="ChartLegend"/>.</summary>
/// <param name="Kind">Picks the swatch.</param>
/// <param name="Label">The words next to the swatch.</param>
public sealed record LegendItem(LegendKind Kind, string Label);
