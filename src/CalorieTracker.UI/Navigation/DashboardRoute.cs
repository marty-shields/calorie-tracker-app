using System.Globalization;

namespace CalorieTracker.UI.Navigation;

/// <summary>The Dashboard's route with the day to show, for screens that send a person to see what they just added.</summary>
public static class DashboardRoute
{
    /// <summary>The route that opens the Dashboard on <paramref name="date"/> (<c>/?date=yyyy-MM-dd</c>).</summary>
    public static string Url(DateOnly date) => $"/?date={date.ToString(DateFormat, CultureInfo.InvariantCulture)}";

    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>The day named by a <c>date</c> query value in yyyy-MM-dd form; null when it is missing or not a date, which opens today.</summary>
    public static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
