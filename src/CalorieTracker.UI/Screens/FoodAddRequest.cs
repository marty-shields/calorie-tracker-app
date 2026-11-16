using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Screens;

/// <summary>A food chosen to add, with the search it came from.</summary>
/// <param name="Filter">The type chosen on the chips; null is All.</param>
public sealed record FoodAddRequest(Food Food, string Query, FoodType? Filter);
