using System.Globalization;

namespace CalorieTracker.UI.Navigation;

/// <summary>The one yyyy-MM-dd form a day takes in a route's <c>date</c> query value, so every route builds and parses it the same way.</summary>
public static class RouteDate
{
    private const string Format = "yyyy-MM-dd";

    /// <summary>The query text for <paramref name="date"/>, such as 2031-07-04.</summary>
    public static string ToText(DateOnly date) => date.ToString(Format, CultureInfo.InvariantCulture);

    /// <summary>The day named by a query value in yyyy-MM-dd form; null when it is missing or not a date.</summary>
    public static DateOnly? Parse(string? text) =>
        DateOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
