using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Summary;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Trends;

/// <summary>
/// Builds the figures of the Trends screen for a range of days from one read of the range's kept entries and their foods and of the streak (<see cref="TrendsStore"/>) and one read of the targets each day had (<see cref="TargetService.ForRangeAsync"/>).
/// Nothing throws: a failing database, an entry whose food is gone or a figure too large gives <see cref="TrendsStatus.Failed"/>.
/// Logs carry the range length and counts only, never a food name, title or calories.
/// </summary>
public sealed partial class TrendsService
{
    /// <summary>Logged days a figure needs before it shows, an assumption to confirm with the owner.</summary>
    public const int MinDays = 3;

    /// <summary>Shortest range (in days) that is split into buckets; a week or less has none.</summary>
    private const int WeekDays = 7;

    /// <summary>Longest range split into weeks; a longer one is split into months.</summary>
    private const int LongestWeekRangeDays = 62;

    private const int TopFoodCount = 5;

    private readonly TrendsStore store;
    private readonly TargetService targets;
    private readonly ILogger<TrendsService> logger;

    internal TrendsService(TrendsStore store, TargetService targets, ILogger<TrendsService> logger)
    {
        this.store = store;
        this.targets = targets;
        this.logger = logger;
    }

    /// <summary>
    /// The trends of the days from <paramref name="from"/> to <paramref name="to"/> inclusive, judged as of <paramref name="today"/>.
    /// Today is in progress and a later day is in the future; both stay out of the averages, counts and top foods, and only today (when it has an entry) counts to the streak.
    /// Each past day is judged against the target it had: on target within 10% of it (edges included), under below that, over above it.
    /// A range that ends before it starts or is longer than <see cref="TargetService.MaxRangeDays"/> days gives <see cref="TrendsStatus.Invalid"/>.
    /// </summary>
    public async Task<TrendsResult> GetAsync(DateOnly from, DateOnly to, DateOnly today)
    {
        if (to < from || to.DayNumber - from.DayNumber >= TargetService.MaxRangeDays)
        {
            Log.Rejected(logger);
            return TrendsResult.Invalid();
        }

        try
        {
            var targetRange = await targets.ForRangeAsync(from, to);
            if (targetRange.Status != TargetStatus.Ok)
            {
                return TrendsResult.Failed();
            }

            var read = await store.ReadAsync(DiaryEntry.FormatDate(from), DiaryEntry.FormatDate(to), today);
            var report = Build(from, to, today, targetRange.Days, read);
            if (report is null)
            {
                return TrendsResult.Failed();
            }

            Log.Built(logger, report.Days.Count, read.Entries.Count);
            return TrendsResult.Ok(report);
        }
        catch (OverflowException)
        {
            Log.Overflow(logger);
            return TrendsResult.Failed();
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return TrendsResult.Failed();
        }
    }

    private TrendsReport? Build(DateOnly from, DateOnly to, DateOnly today, IReadOnlyList<DayTarget> dayTargets, TrendsRead read)
    {
        var byDate = new Dictionary<string, List<(DiaryEntry Entry, Food Food, Nutrition Nutrition)>>();
        foreach (var entry in read.Entries)
        {
            if (!read.Foods.TryGetValue(entry.FoodId, out var food))
            {
                Log.FoodMissing(logger, entry.Id, entry.FoodId);
                return null;
            }

            if (!Enum.IsDefined(entry.Meal))
            {
                Log.MealUnknown(logger, entry.Id);
                return null;
            }

            if (!byDate.TryGetValue(entry.Date, out var list))
            {
                byDate[entry.Date] = list = [];
            }

            list.Add((entry, food, Nutrition.ForAmount(food, entry.Amount)));
        }

        var days = new List<TrendDay>(dayTargets.Count);
        var foodTotals = new Dictionary<int, (Food Food, int Times, int Kcal)>();
        foreach (var dayTarget in dayTargets)
        {
            var key = DiaryEntry.FormatDate(dayTarget.Date);
            var totals = Nutrition.Zero;
            var meals = MealKcal.Zero;
            var logged = byDate.TryGetValue(key, out var entries);
            foreach (var (entry, food, nutrition) in entries ?? [])
            {
                totals = totals.Plus(nutrition);
                meals = meals.Add(entry.Meal, nutrition.Kcal);
                if (dayTarget.Date < today)
                {
                    var (_, times, kcal) = foodTotals.GetValueOrDefault(food.Id);
                    foodTotals[food.Id] = (food, checked(times + 1), checked(kcal + nutrition.Kcal));
                }
            }

            days.Add(new TrendDay(dayTarget.Date, dayTarget.Kcal, totals, meals, logged, StatusOf(dayTarget, logged, totals.Kcal, today)));
        }

        var past = days.Where(d => d.Date < today).ToList();
        var loggedPast = past.Where(d => d.IsLogged).ToList();
        var judged = loggedPast.Count(d => d.Status is DayStatus.Under or DayStatus.OnTarget or DayStatus.Over);
        var enough = loggedPast.Count >= MinDays;
        var range = new TrendsRange(
            past.Count,
            loggedPast.Count,
            AverageKcal(loggedPast),
            AverageMacros(loggedPast),
            AverageMeals(loggedPast),
            past.Count(d => d.Status == DayStatus.Under),
            past.Count(d => d.Status == DayStatus.OnTarget),
            past.Count(d => d.Status == DayStatus.Over),
            foodTotals
                .OrderByDescending(f => f.Value.Kcal)
                .ThenByDescending(f => f.Value.Times)
                .ThenBy(f => f.Key)
                .Take(TopFoodCount)
                .Select(f => new TopFood(f.Key, FoodDisplay.Of(f.Value.Food.Name, f.Value.Food.Brand, f.Value.Food.Brands, null).Title, f.Value.Times, f.Value.Kcal))
                .ToList(),
            read.Streak,
            new TrendsEnoughData(enough, enough, enough, judged >= MinDays, enough));

        var (kind, buckets) = BucketsOf(days, today, to.DayNumber - from.DayNumber + 1);
        return new TrendsReport(days, range, kind, buckets);
    }

    private static DayStatus StatusOf(DayTarget day, bool logged, int kcal, DateOnly today)
    {
        if (day.Date > today)
        {
            return DayStatus.Future;
        }

        if (day.Date == today)
        {
            return DayStatus.Today;
        }

        if (!logged)
        {
            return DayStatus.NotLogged;
        }

        if (day.Kcal is not { } target)
        {
            return DayStatus.NoTarget;
        }

        // Tenths of the target in whole numbers, so the edges are exact: 1,890 and 2,310 against 2,100.
        return 10L * kcal < 9L * target ? DayStatus.Under
            : 10L * kcal > 11L * target ? DayStatus.Over
            : DayStatus.OnTarget;
    }

    private static int? AverageKcal(List<TrendDay> logged) =>
        logged.Count == 0 ? null : WholeAverage(logged.Sum(d => (long)d.Totals.Kcal), logged.Count);

    private static int WholeAverage(long sum, int count) => checked((int)Math.Round((decimal)sum / count, MidpointRounding.AwayFromZero));

    private static Nutrition? AverageMacros(List<TrendDay> logged)
    {
        if (logged.Count == 0)
        {
            return null;
        }

        decimal Average(Func<Nutrition, double> pick) => logged.Sum(d => Nutrition.ToDecimal(pick(d.Totals))) / logged.Count;
        return new Nutrition(
            AverageKcal(logged)!.Value,
            Nutrition.Round(Average(n => n.ProteinG)),
            Nutrition.Round(Average(n => n.CarbsG)),
            Nutrition.Round(Average(n => n.FatG)));
    }

    private static MealKcal? AverageMeals(List<TrendDay> logged) =>
        logged.Count == 0
            ? null
            : new MealKcal(
                WholeAverage(logged.Sum(d => (long)d.KcalByMeal.Breakfast), logged.Count),
                WholeAverage(logged.Sum(d => (long)d.KcalByMeal.Lunch), logged.Count),
                WholeAverage(logged.Sum(d => (long)d.KcalByMeal.Dinner), logged.Count),
                WholeAverage(logged.Sum(d => (long)d.KcalByMeal.Snacks), logged.Count));

    private static (TrendBucketKind Kind, IReadOnlyList<TrendBucket> Buckets) BucketsOf(IReadOnlyList<TrendDay> days, DateOnly today, int length)
    {
        if (length <= WeekDays)
        {
            return (TrendBucketKind.None, []);
        }

        var weeks = length <= LongestWeekRangeDays;
        var groups = days.GroupBy(d => weeks ? WeekStart(d.Date) : new DateOnly(d.Date.Year, d.Date.Month, 1));
        var buckets = groups.Select(g =>
        {
            var logged = g.Where(d => d.Date < today && d.IsLogged).ToList();
            return new TrendBucket(g.First().Date, g.Last().Date, AverageKcal(logged), logged.Count, g.Count(d => d.Status == DayStatus.Over));
        }).ToList();
        return (weeks ? TrendBucketKind.Week : TrendBucketKind.Month, buckets);
    }

    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.TrendsBuilt, LogLevel.Debug, "Trends built for {Days} days from {Entries} entries")]
        public static partial void Built(ILogger logger, int days, int entries);

        [LoggerMessage((int)CoreEvent.TrendsRejected, LogLevel.Debug, "Trends range rejected as backwards or too long")]
        public static partial void Rejected(ILogger logger);

        [LoggerMessage((int)CoreEvent.TrendsFoodMissing, LogLevel.Warning, "Trends not built, entry {EntryId} points at missing food {FoodId}")]
        public static partial void FoodMissing(ILogger logger, int entryId, int foodId);

        [LoggerMessage((int)CoreEvent.TrendsMealUnknown, LogLevel.Warning, "Trends not built, entry {EntryId} has a meal that does not exist")]
        public static partial void MealUnknown(ILogger logger, int entryId);

        [LoggerMessage((int)CoreEvent.TrendsOverflow, LogLevel.Warning, "Trends not built, a figure was too large")]
        public static partial void Overflow(ILogger logger);

        [LoggerMessage((int)CoreEvent.TrendsFailed, LogLevel.Warning, "Trends call failed")]
        public static partial void Failed(ILogger logger);
    }
}
