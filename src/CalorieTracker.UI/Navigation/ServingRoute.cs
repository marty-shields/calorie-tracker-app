using System.Globalization;
using CalorieTracker.Core.Data;
using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Navigation;

/// <summary>The Serving screen's route and the query parameters it reads, so every entry point builds and every read parses them the same way.</summary>
public static class ServingRoute
{
    public const string Template = "/serving/{FoodId:int}";

    /// <summary>Where Back goes when the route names no return screen.</summary>
    public const string DefaultBack = "/foods";

    /// <summary>Where a saved entry goes when the route names no return screen.</summary>
    public const string DefaultAfterAdd = "/";

    /// <summary>The route to add a food. Leave <paramref name="meal"/> null to suggest a meal from the time of day, and <paramref name="date"/> null for today.</summary>
    /// <param name="returnTo">An in-app route such as /foods to go to after Back or a saved entry; null for the defaults. A route here also makes the screen offer "View day" after Add instead of Undo.</param>
    /// <param name="backTo">An in-app route for Back alone, when it differs from where a saved entry goes; null uses <paramref name="returnTo"/>.</param>
    /// <param name="amount">Grams or ml to open with instead of the default, such as what was last logged of the food; null for the default.</param>
    /// <param name="servings">The count of servings to open with, with <paramref name="amount"/>; null opens by weight.</param>
    /// <param name="servingId">The serving the count is in, with <paramref name="servings"/>; null opens on the smallest.</param>
    public static string Url(int foodId, Meal? meal = null, DateOnly? date = null, string? returnTo = null, string? backTo = null, double? amount = null, double? servings = null, int? servingId = null)
    {
        var query = new List<string>();
        if (meal is { } m)
        {
            query.Add($"meal={m}");
        }

        if (date is { } d)
        {
            query.Add($"date={RouteDate.ToText(d)}");
        }

        if (returnTo is not null)
        {
            query.Add($"return={Uri.EscapeDataString(returnTo)}");
        }

        if (backTo is not null)
        {
            query.Add($"back={Uri.EscapeDataString(backTo)}");
        }

        if (amount is { } a)
        {
            query.Add($"amount={Text(a)}");
            if (servings is { } count)
            {
                query.Add($"servings={Text(count)}");
                if (servingId is { } serving)
                {
                    query.Add($"serving={serving.ToString(CultureInfo.InvariantCulture)}");
                }
            }
        }

        return $"/serving/{foodId}" + (query.Count == 0 ? "" : "?" + string.Join('&', query));
    }

    /// <summary>The route to change or remove a kept entry of a food: the entry's id, day, meal and amounts travel in the query, since a single entry can't be read back.</summary>
    /// <param name="amount">Grams or ml.</param>
    /// <param name="servings">The count of servings the entry was added by; null when it was added by weight.</param>
    /// <param name="servingId">The serving the count is in; null when the entry has none.</param>
    public static string EditUrl(int entryId, int foodId, Meal meal, DateOnly date, double amount, double? servings = null, int? servingId = null) =>
        $"/serving/{foodId}?entry={entryId}&meal={meal}&date={RouteDate.ToText(date)}&amount={Text(amount)}"
        + (servings is { } count ? $"&servings={Text(count)}" : "")
        + (servings is not null && servingId is { } serving ? $"&serving={serving.ToString(CultureInfo.InvariantCulture)}" : "");

    /// <summary>A whole number above zero in a query value, or null when it is missing or not one.</summary>
    public static int? ParseId(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;

    private static string Text(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A positive, finite number in a query value (a point as the decimal separator), or null when it is missing or not one.</summary>
    public static double? ParseNumber(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value > 0 ? value : null;

    /// <summary>The meal named by a query value, or null when it is missing or not one of the four.</summary>
    public static Meal? ParseMeal(string? text) =>
        Enum.GetNames<Meal>().FirstOrDefault(name => name.Equals(text, StringComparison.OrdinalIgnoreCase)) is { } found ? Enum.Parse<Meal>(found) : null;

    /// <summary>The day named by a query value in yyyy-MM-dd form, or <paramref name="today"/> when it is missing or not a date.</summary>
    public static DateOnly ParseDate(string? text, DateOnly today) => RouteDate.Parse(text) ?? today;

    /// <summary>The return route when it is an in-app path (starts with one slash, no backslash or control character), otherwise <paramref name="fallback"/>.</summary>
    public static string ReturnOr(string? returnTo, string fallback) =>
        returnTo is { Length: > 0 } && returnTo[0] == '/' && !returnTo.StartsWith("//", StringComparison.Ordinal) && !returnTo.Contains('\\') && !returnTo.Any(char.IsControl) ? returnTo : fallback;

    /// <summary>Where Back goes: the <c>back</c> route if it is in-app, else the <c>return</c> route if it is, else <see cref="DefaultBack"/>.</summary>
    public static string BackTarget(string? backTo, string? returnTo) => ReturnOr(backTo, ReturnOr(returnTo, DefaultBack));

    /// <summary>Where a saved entry goes: the <c>return</c> route if it is in-app, else the Dashboard on <paramref name="date"/> when the route names a day, else <see cref="DefaultAfterAdd"/>.</summary>
    public static string AfterAddTarget(string? returnTo, DateOnly? date = null) =>
        ReturnOr(returnTo, date is { } day ? DashboardRoute.Url(day) : DefaultAfterAdd);

    /// <summary>View day when the <c>return</c> route is accepted, since the person stays where they came from; Undo when it is missing or rejected, since a saved entry then goes to the Dashboard.</summary>
    public static AddedFollowUp FollowUpFor(string? returnTo) =>
        ReturnOr(returnTo, "") is { Length: > 0 } ? AddedFollowUp.ViewDay : AddedFollowUp.Undo;
}
