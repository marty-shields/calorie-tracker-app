using System.Globalization;

namespace CalorieTracker.UI.Components.Inputs;

/// <summary>The wording of a day on the date strip and under a screen's title: "Today, 18 Nov", "Yesterday, 17 Nov", "Tomorrow, 19 Nov", else "Sat, 14 Nov".</summary>
public static class DayLabel
{
    /// <summary>The year is added only for a date outside <paramref name="today"/>'s year.</summary>
    public static string For(DateOnly date, DateOnly today)
    {
        var text = date.ToString(date.Year == today.Year ? "d MMM" : "d MMM yyyy", CultureInfo.InvariantCulture);
        return (date.DayNumber - today.DayNumber) switch
        {
            0 => $"Today, {text}",
            -1 => $"Yesterday, {text}",
            1 => $"Tomorrow, {text}",
            _ => $"{date.ToString("ddd", CultureInfo.InvariantCulture)}, {text}",
        };
    }
}
