namespace CalorieTracker.UI.Components.Charts;

/// <summary>One logged (or not logged) day of a <see cref="MonthCalendar"/>.</summary>
/// <param name="Date">Matched against each day of the month; a date outside the month is ignored.</param>
/// <param name="Value">Calories. Null, NaN, infinity, or zero or less, means nothing was logged.</param>
/// <param name="Target">The target that day had, when it differs from the calendar's.</param>
public sealed record CalendarDay(DateOnly Date, double? Value, double? Target = null);
