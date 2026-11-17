using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.UI.Models.Serving;

/// <summary>The checks the Serving page and view share, so what the screen shows and what Add saves never disagree.</summary>
public static class ServingRules
{
    /// <summary>True when the food has at least one serving, which is what shows the serving switch.</summary>
    public static bool HasServing(IReadOnlyList<CalorieTracker.Core.Data.Serving>? servings) => servings is { Count: > 0 };

    /// <summary>The serving to open on: the one with this id (such as the one last logged), else the smallest, the first when two are as small. Null when there are none.</summary>
    public static CalorieTracker.Core.Data.Serving? Pick(IReadOnlyList<CalorieTracker.Core.Data.Serving> servings, int? id) =>
        servings.FirstOrDefault(s => s.Id == id)
        ?? servings.MinBy(s => s.Amount);

    /// <summary>True when the weight is above zero and at most <see cref="DiaryService.MaxAmount"/>.</summary>
    public static bool IsValidGrams(double? grams) => grams is > 0 and <= DiaryService.MaxAmount;

    /// <summary>The error under the name field of the own-serving form: null while it is empty (Save is off) or fine, a message when it is longer than <see cref="FoodService.MaxServingLabelLength"/> after trimming.</summary>
    public static string? OwnLabelMessage(string? label) =>
        label?.Trim().Length > FoodService.MaxServingLabelLength ? $"Use {FoodService.MaxServingLabelLength} characters or fewer." : null;

    /// <summary>The error under the size field of the own-serving form: null while it is empty or fine; "Enter an amount above 0." for zero or less, and the largest size when it is over <see cref="FoodService.MaxServingAmount"/>.</summary>
    public static string? OwnAmountMessage(decimal? amount, string unit) =>
        amount switch
        {
            null => null,
            <= 0 => "Enter an amount above 0.",
            var value when (double)value > FoodService.MaxServingAmount => $"Enter an amount of {FoodService.MaxServingAmount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} {unit} or less.",
            _ => null,
        };
}
