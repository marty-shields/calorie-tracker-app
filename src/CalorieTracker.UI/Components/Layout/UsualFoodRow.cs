namespace CalorieTracker.UI.Components.Layout;

/// <summary>One usual food on the Add sheet, ready to show.</summary>
/// <param name="Detail">The last amount and its kcal, such as "150 g · 222 kcal".</param>
public sealed record UsualFoodRow(int FoodId, string Title, string Detail);

/// <summary>A usual food's + tapped on the Add sheet, with the meal chosen there.</summary>
public sealed record UsualAddRequest(UsualFoodRow Food, CalorieTracker.Core.Data.Meal Meal);
