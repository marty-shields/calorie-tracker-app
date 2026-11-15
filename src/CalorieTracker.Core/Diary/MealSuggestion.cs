using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Diary;

/// <summary>Suggests the meal to add a food to when none was chosen, from the time of day.</summary>
public static class MealSuggestion
{
    /// <summary>Breakfast before 11:00, Lunch from 11:00 to before 15:00, Dinner from 15:00 to before 21:00, Snacks from 21:00 on.</summary>
    /// <param name="time">The person's local time of day.</param>
    public static Meal For(TimeOnly time) =>
        time.Hour switch
        {
            < 11 => Meal.Breakfast,
            < 15 => Meal.Lunch,
            < 21 => Meal.Dinner,
            _ => Meal.Snacks,
        };
}
