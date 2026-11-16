using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Screens;

/// <summary>A food chosen to add, with the search it came from.</summary>
public sealed record FoodAddRequest(Food Food, string Query);
