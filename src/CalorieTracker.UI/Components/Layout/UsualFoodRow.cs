using System.Globalization;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Summary;

namespace CalorieTracker.UI.Components.Layout;

/// <summary>One usual food on the Add sheet, ready to show.</summary>
/// <param name="Detail">The last amount and its kcal, such as "150 g · 222 kcal".</param>
/// <param name="LastAmount">What a + adds: the last amount in the food's unit.</param>
/// <param name="LastServings">What a + adds: the last servings, null when the last entry was by amount.</param>
/// <param name="LastServingId">What a + adds: the serving of <paramref name="LastServings"/>.</param>
public sealed record UsualFoodRow(int FoodId, string Title, string Detail, double LastAmount = 0, double? LastServings = null, int? LastServingId = null)
{
    /// <summary>The row for <paramref name="food"/> with its last use. The detail ends in kcal; for servings it names the serving (the amount when the serving is gone).</summary>
    public static UsualFoodRow From(Food food, IReadOnlyList<Serving> servings, FoodHistory last)
    {
        var label = last.LastServings is null ? null : servings.FirstOrDefault(s => s.Id == last.LastServingId)?.Label;
        var kcal = Nutrition.ForAmount(food, last.LastAmount).Kcal.ToString("N0", CultureInfo.InvariantCulture);
        var detail = $"{AmountText.Of(last.LastAmount, food.Unit, last.LastServings, label)} · {kcal} kcal";
        return new UsualFoodRow(food.Id, FoodDisplay.Of(food).Title, detail, last.LastAmount, last.LastServings, last.LastServingId);
    }
}

/// <summary>A usual food's + tapped on the Add sheet, with the meal chosen there.</summary>
public sealed record UsualAddRequest(UsualFoodRow Food, CalorieTracker.Core.Data.Meal Meal);
