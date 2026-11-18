using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Navigation;

/// <summary>Food search's route and its query, so a screen that comes back to it restores the search, and a meal's + opens it in add mode. An old link may still carry a <c>type</c> value; it is ignored.</summary>
public static class FoodSearchRoute
{
    public const string Template = "/foods";

    /// <summary>The route that shows a search again.</summary>
    public static string Url(string query) =>
        string.IsNullOrEmpty(query) ? Template : $"{Template}?q={Uri.EscapeDataString(query)}";

    /// <summary>The route that opens search to add to <paramref name="meal"/>; <paramref name="date"/> null is today. A non-null <paramref name="query"/> also restores that search.</summary>
    public static string AddUrl(Meal meal, DateOnly? date = null, string? query = null)
    {
        var url = $"{Template}?{RouteDate.MealQuery(meal, date)}";
        return string.IsNullOrEmpty(query) ? url : url + $"&q={Uri.EscapeDataString(query)}";
    }
}
