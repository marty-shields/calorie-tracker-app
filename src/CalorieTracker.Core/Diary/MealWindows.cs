using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Diary;

/// <summary>Where a meal stands against its window at a time of day.</summary>
public enum MealWindowState
{
    /// <summary>The window has not opened yet.</summary>
    Before,

    /// <summary>The window is open: its start is included, its end is not.</summary>
    During,

    /// <summary>The window has closed.</summary>
    After,

    /// <summary>The meal has no window (Snacks).</summary>
    AnyTime,
}

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

    /// <summary>Whether <paramref name="meal"/>'s window is still ahead, open or closed at <paramref name="time"/>; <see cref="MealWindowState.AnyTime"/> for Snacks.</summary>
    public static MealWindowState StateFor(Meal meal, TimeOnly time)
    {
        if (meal == Meal.Snacks)
        {
            return MealWindowState.AnyTime;
        }

        var (start, end) = WindowOf(meal);
        return time < start ? MealWindowState.Before : time < end ? MealWindowState.During : MealWindowState.After;
    }

    /// <summary>When <paramref name="meal"/>'s window opens. Snacks has no window, so it is an <see cref="ArgumentOutOfRangeException"/>.</summary>
    public static TimeOnly StartOf(Meal meal) => WindowOf(meal).Start;

    private static (TimeOnly Start, TimeOnly End) WindowOf(Meal meal) => meal switch
    {
        Meal.Breakfast => (BreakfastStart, LunchStart),
        Meal.Lunch => (LunchStart, LunchEnd),
        Meal.Dinner => (DinnerStart, DinnerEnd),
        _ => throw new ArgumentOutOfRangeException(nameof(meal), meal, "Snacks has no window."),
    };

    private static bool InWindow(TimeOnly time, TimeOnly start, TimeOnly end) => time >= start && time < end;
}
