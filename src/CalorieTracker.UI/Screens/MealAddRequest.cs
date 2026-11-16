using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Screens;

/// <summary>A meal's + tapped on the Dashboard: the meal, and the day on the date strip.</summary>
public sealed record MealAddRequest(Meal Meal, DateOnly Date);
