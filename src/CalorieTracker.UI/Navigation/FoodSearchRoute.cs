using System.Globalization;
using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Navigation;

/// <summary>Food search's route and its query, so a screen that comes back to it restores the search, and a meal's + opens it in add mode.</summary>
public static class FoodSearchRoute
{
    public const string Template = "/foods";

    private const string AllType = "All";

    /// <summary>The route that shows a search again. <paramref name="filter"/> null is All.</summary>
    public static string Url(string query, FoodType? filter)
    {
        var type = filter?.ToString() ?? AllType;
        return string.IsNullOrEmpty(query) ? $"{Template}?type={type}" : $"{Template}?q={Uri.EscapeDataString(query)}&type={type}";
    }

    /// <summary>The route that opens search to add to <paramref name="meal"/>; <paramref name="date"/> null is today.</summary>
    public static string AddUrl(Meal meal, DateOnly? date = null) =>
        $"{Template}?meal={meal}" + (date is { } d ? $"&date={d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : "");

    /// <summary>The type chosen by a query value: All is null, a type name is that type, anything else (or nothing) is Ingredients, where search opens.</summary>
    public static FoodType? ParseFilter(string? text)
    {
        if (AllType.Equals(text, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.GetNames<FoodType>().FirstOrDefault(name => name.Equals(text, StringComparison.OrdinalIgnoreCase)) is { } found
            ? Enum.Parse<FoodType>(found)
            : FoodType.Ingredient;
    }
}
