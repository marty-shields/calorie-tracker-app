using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Screens;

/// <summary>A food chosen to add, with the search it came from.</summary>
/// <param name="LastAmount">Grams or millilitres last logged of the food (in the meal being added to when it was logged there); null for a food never logged.</param>
/// <param name="LastServings">Servings of that last entry; null when it was entered by weight or the food was never logged.</param>
public sealed record FoodAddRequest(Food Food, string Query, double? LastAmount = null, double? LastServings = null);
