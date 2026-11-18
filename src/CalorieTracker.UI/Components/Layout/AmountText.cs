using System.Globalization;
using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Components.Layout;

/// <summary>The words for an amount of food, shared by the day's entries and the usual foods.</summary>
public static class AmountText
{
    public static string Count(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The amount such as "150 g"; for an entry added by servings "1 serving · the serving's label" (the amount when the label is missing).</summary>
    public static string Of(double amount, FoodUnit unit, double? servings, string? servingLabel)
    {
        var text = $"{Count(amount)} {(unit == FoodUnit.Millilitre ? "ml" : "g")}";
        if (servings is not { } count)
        {
            return text;
        }

        var servingText = $"{Count(count)} {(Math.Abs(count - 1) < 0.005 ? "serving" : "servings")}";
        return $"{servingText} · {(string.IsNullOrWhiteSpace(servingLabel) ? text : servingLabel)}";
    }
}
