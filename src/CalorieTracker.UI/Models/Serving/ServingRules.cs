using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.UI.Models.Serving;

/// <summary>The checks the Serving page and view share, so what the screen shows and what Add saves never disagree.</summary>
public static class ServingRules
{
    /// <summary>True when the food has a label and a weight above zero, up to <see cref="FoodService.MaxServingAmount"/>, which is what shows the serving switch.</summary>
    public static bool HasServing(Food? food) =>
        food is { ServingAmount: > 0 and <= FoodService.MaxServingAmount } && !string.IsNullOrWhiteSpace(food.ServingLabel);

    /// <summary>True when the weight is above zero and at most <see cref="DiaryService.MaxAmount"/>.</summary>
    public static bool IsValidGrams(double? grams) => grams is > 0 and <= DiaryService.MaxAmount;
}
