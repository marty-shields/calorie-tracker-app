using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Navigation;

/// <summary>Scan's route and its query, as <see cref="FoodSearchRoute"/> does for search: a meal and a day make the scan add to that meal on that day.</summary>
public static class ScanRoute
{
    public const string Template = "/scan";

    /// <summary>The route that opens the scanner to add to <paramref name="meal"/>; <paramref name="date"/> null is today.</summary>
    /// <param name="backTo">An in-app route Back returns to, such as the search that opened the scanner; null returns to the Dashboard.</param>
    public static string AddUrl(Meal meal, DateOnly? date = null, string? backTo = null)
    {
        var url = $"{Template}?{RouteDate.MealQuery(meal, date)}";
        return backTo is null ? url : url + $"&back={Uri.EscapeDataString(backTo)}";
    }

    /// <summary>Where Back goes: the <c>back</c> route if it is in-app, else the Dashboard on <paramref name="date"/> (today when null).</summary>
    public static string BackTarget(string? backTo, DateOnly? date) =>
        ServingRoute.ReturnOr(backTo, DashboardRoute.Url(date));

    /// <summary>Where Search by name instead goes: back to the <c>back</c> route when it is a search (so the typed text survives), else a new search for <paramref name="meal"/> and <paramref name="date"/>.</summary>
    public static string SearchTarget(string? backTo, Meal meal, DateOnly? date) =>
        ServingRoute.ReturnOr(backTo, "") is { } back && back.StartsWith(FoodSearchRoute.Template + "?", StringComparison.Ordinal) ? back : FoodSearchRoute.AddUrl(meal, date);
}
