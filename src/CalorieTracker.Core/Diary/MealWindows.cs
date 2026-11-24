using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Diary;

/// <summary>
/// The fixed times of day of each meal: breakfast 06:00 to 11:00, lunch 11:00 to 15:00, dinner 15:00 to 20:00, snacks at any time.
/// A window includes its start and not its end, so breakfast runs to 10:59, lunch to 14:59 and dinner to 19:59. Pure: it reads no clock, so the caller passes the person's local time.
/// </summary>
public static class MealWindows
{
    private static readonly TimeOnly BreakfastStart = new(6, 0);
    private static readonly TimeOnly LunchStart = new(11, 0);
    private static readonly TimeOnly LunchEnd = new(15, 0);
    private static readonly TimeOnly DinnerStart = new(15, 0);
    private static readonly TimeOnly DinnerEnd = new(20, 0);

    /// <summary>The meal whose window holds <paramref name="time"/>; Snacks before 06:00 and from 20:00 on.</summary>
    public static Meal MealFor(TimeOnly time) =>
        InWindow(time, BreakfastStart, LunchStart) ? Meal.Breakfast
        : InWindow(time, LunchStart, LunchEnd) ? Meal.Lunch
        : InWindow(time, DinnerStart, DinnerEnd) ? Meal.Dinner
        : Meal.Snacks;

    private static bool InWindow(TimeOnly time, TimeOnly start, TimeOnly end) => time >= start && time < end;
}
