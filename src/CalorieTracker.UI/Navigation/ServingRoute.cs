using System.Globalization;
using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Navigation;

/// <summary>The Serving screen's route and the query parameters it reads, so every entry point builds and every read parses them the same way.</summary>
public static class ServingRoute
{
    public const string Template = "/serving/{FoodId:int}";

    /// <summary>Where Back goes when the route names no return screen.</summary>
    public const string DefaultBack = "/foods";

    /// <summary>Where a saved entry goes when the route names no return screen.</summary>
    public const string DefaultAfterAdd = "/";

    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>The route to add a food. Leave <paramref name="meal"/> null to suggest a meal from the time of day, and <paramref name="date"/> null for today.</summary>
    /// <param name="returnTo">An in-app route such as /foods to go back to; null for the defaults.</param>
    public static string Url(int foodId, Meal? meal = null, DateOnly? date = null, string? returnTo = null)
    {
        var query = new List<string>();
        if (meal is { } m)
        {
            query.Add($"meal={m}");
        }

        if (date is { } d)
        {
            query.Add($"date={d.ToString(DateFormat, CultureInfo.InvariantCulture)}");
        }

        if (returnTo is not null)
        {
            query.Add($"return={Uri.EscapeDataString(returnTo)}");
        }

        return $"/serving/{foodId}" + (query.Count == 0 ? "" : "?" + string.Join('&', query));
    }

    /// <summary>The meal named by a query value, or null when it is missing or not one of the four.</summary>
    public static Meal? ParseMeal(string? text) =>
        Enum.GetNames<Meal>().FirstOrDefault(name => name.Equals(text, StringComparison.OrdinalIgnoreCase)) is { } found ? Enum.Parse<Meal>(found) : null;

    /// <summary>The day named by a query value in yyyy-MM-dd form, or <paramref name="today"/> when it is missing or not a date.</summary>
    public static DateOnly ParseDate(string? text, DateOnly today) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : today;

    /// <summary>The return route when it is an in-app path (starts with one slash), otherwise <paramref name="fallback"/>.</summary>
    public static string ReturnOr(string? returnTo, string fallback) =>
        returnTo is { Length: > 0 } && returnTo[0] == '/' && !returnTo.StartsWith("//", StringComparison.Ordinal) && !returnTo.Contains('\\') ? returnTo : fallback;
}
