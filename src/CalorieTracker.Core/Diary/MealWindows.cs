using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Diary;

/// <summary>
/// The fixed times of day of each meal: breakfast 05:00 to 11:00, lunch 11:00 to 15:00, dinner 17:00 to 21:00, snacks at any time.
/// A window includes its start and not its end. Pure: it reads no clock, so the caller passes the person's local time.
/// </summary>
public static class MealWindows
{
    private static readonly TimeOnly BreakfastStart = new(5, 0);
    private static readonly TimeOnly LunchStart = new(11, 0);
    private static readonly TimeOnly LunchEnd = new(15, 0);
    private static readonly TimeOnly DinnerStart = new(17, 0);
    private static readonly TimeOnly DinnerEnd = new(21, 0);

    /// <summary>The meal whose window holds <paramref name="time"/>; Snacks between the windows (15:00 to 17:00) and overnight (21:00 to 05:00).</summary>
    public static Meal MealFor(TimeOnly time) =>
        InWindow(time, BreakfastStart, LunchStart) ? Meal.Breakfast
        : InWindow(time, LunchStart, LunchEnd) ? Meal.Lunch
        : InWindow(time, DinnerStart, DinnerEnd) ? Meal.Dinner
        : Meal.Snacks;

    private static bool InWindow(TimeOnly time, TimeOnly start, TimeOnly end) => time >= start && time < end;
}
