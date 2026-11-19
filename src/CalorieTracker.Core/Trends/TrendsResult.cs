using CalorieTracker.Core.Data;
using CalorieTracker.Core.Summary;

namespace CalorieTracker.Core.Trends;

public enum TrendsStatus
{
    Ok,
    Invalid,
    Failed,
}

/// <summary>How a day compares with the target it had. A day is on target within 10% either side of the target, the edges included.</summary>
public enum DayStatus
{
    Under,
    OnTarget,
    Over,
    NoTarget,
    NotLogged,
    Future,
    Today,
}

/// <summary>Whole kcal for each meal.</summary>
public sealed record MealKcal(int Breakfast, int Lunch, int Dinner, int Snacks)
{
    public static MealKcal Zero { get; } = new(0, 0, 0, 0);

    internal MealKcal Add(Meal meal, int kcal) =>
        meal switch
        {
            Meal.Breakfast => this with { Breakfast = checked(Breakfast + kcal) },
            Meal.Lunch => this with { Lunch = checked(Lunch + kcal) },
            Meal.Dinner => this with { Dinner = checked(Dinner + kcal) },
            _ => this with { Snacks = checked(Snacks + kcal) },
        };
}

/// <summary>One day of the range.</summary>
/// <param name="TargetKcal">The target the day had; null when none.</param>
/// <param name="Totals">Energy and macros of the day's kept entries, summed as <see cref="DaySummary"/> does; zero when nothing was logged.</param>
/// <param name="Status">Today is <see cref="DayStatus.Today"/> whether or not it has entries, and a later day <see cref="DayStatus.Future"/>.</param>
public sealed record TrendDay(DateOnly Date, int? TargetKcal, Nutrition Totals, MealKcal KcalByMeal, bool IsLogged, DayStatus Status);

/// <summary>A food's place in the top foods. <see cref="Kcal"/> is the sum of its entries' whole kcal.</summary>
public sealed record TopFood(int FoodId, string Title, int TimesEaten, int Kcal);

/// <summary>Whether each figure rests on enough days to show. A figure with its flag false should read "Not enough days yet".</summary>
/// <param name="Counts">Under, on target and over: needs <see cref="TrendsService.MinDays"/> judged days (logged, with a target).</param>
public sealed record TrendsEnoughData(bool Average, bool Macros, bool ByMeal, bool Counts, bool TopFoods);

/// <summary>The figures of the range over its past days (before today). Averages are null with no logged past day.</summary>
/// <param name="PastDays">Days of the range before today.</param>
/// <param name="DaysLogged">Past days with at least one kept entry.</param>
/// <param name="AverageKcal">Whole kcal a logged past day.</param>
/// <param name="AverageMacros">Grams to one decimal; <see cref="Nutrition.Kcal"/> repeats <paramref name="AverageKcal"/>.</param>
/// <param name="TopFoods">At most 5, most kcal first, then most times eaten, then lowest food id.</param>
/// <param name="Streak">Days in a row with an entry, back from today, or from yesterday when today is empty. It looks past the range's start and counts through any number of days, stops at the first day with no entry, and never counts a day after today.</param>
public sealed record TrendsRange(
    int PastDays,
    int DaysLogged,
    int? AverageKcal,
    Nutrition? AverageMacros,
    MealKcal? AverageKcalByMeal,
    int Under,
    int OnTarget,
    int Over,
    IReadOnlyList<TopFood> TopFoods,
    int Streak,
    TrendsEnoughData Enough);

public enum TrendBucketKind
{
    None,
    Week,
    Month,
}

/// <summary>A week (Monday start) or calendar month of the range, clipped to the range.</summary>
/// <param name="AverageKcal">Whole kcal a logged past day in the bucket; null when none is logged.</param>
public sealed record TrendBucket(DateOnly Start, DateOnly End, int? AverageKcal, int DaysLogged, int DaysOver);

/// <summary>Everything the Trends screen shows for a range.</summary>
/// <param name="Days">One for each day, oldest first.</param>
/// <param name="AnyLoggedBeforeToday">Whether any day before today was ever logged, in or out of the range; false is the "nothing logged yet" case.</param>
/// <param name="BucketKind">Weeks for a range of up to 62 days and more than 7, months for a longer one, none for a week or less.</param>
public sealed record TrendsReport(IReadOnlyList<TrendDay> Days, TrendsRange Range, bool AnyLoggedBeforeToday, TrendBucketKind BucketKind, IReadOnlyList<TrendBucket> Buckets);

/// <summary>The outcome of a trends call. <see cref="Report"/> is set only when <see cref="Status"/> is <see cref="TrendsStatus.Ok"/>.</summary>
public sealed record TrendsResult
{
    private TrendsResult(TrendsStatus status, TrendsReport? report)
    {
        Status = status;
        Report = report;
    }

    public TrendsStatus Status { get; }

    public TrendsReport? Report { get; }

    public static TrendsResult Ok(TrendsReport report) => new(TrendsStatus.Ok, report);

    public static TrendsResult Invalid() => new(TrendsStatus.Invalid, null);

    public static TrendsResult Failed() => new(TrendsStatus.Failed, null);
}
