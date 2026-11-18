using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Navigation;

/// <summary>Scan's route and its query, as <see cref="FoodSearchRoute"/> does for search: a meal and a day make the scan add to that meal on that day.</summary>
public static class ScanRoute
{
    public const string Template = "/scan";

    /// <summary>The route that opens the scanner to add to <paramref name="meal"/>; <paramref name="date"/> null is today.</summary>
    public static string AddUrl(Meal meal, DateOnly? date = null) =>
        $"{Template}?{RouteDate.MealQuery(meal, date)}";
}
