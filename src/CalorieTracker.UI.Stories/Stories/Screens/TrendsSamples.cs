using CalorieTracker.Core.Summary;
using CalorieTracker.Core.Trends;

namespace CalorieTracker.UI.Stories.Stories.Screens;

/// <summary>
/// Sample periods for the Trends stories, built the way <c>TrendsService</c> builds a report. Today is 18 Nov 2026 and the target is 2,100,
/// so on target is 1,890 to 2,310. The week of 9 to 15 Nov is the TR2 fixture: average 2,124, 3 of 7 days on target, Friday and Saturday over, Wednesday and Sunday under.
/// </summary>
public static class TrendsSamples
{
    public static DateOnly Today { get; } = new(2026, 11, 18);

    public const int Target = 2100;

    private static readonly int[] FixtureWeek = [1980, 2240, 1875, 2050, 2390, 2610, 1720];

    private static IReadOnlyList<TopFood> TopFoods { get; } =
    [
        new(1, "Chicken curry", 6, 3180),
        new(2, "Porridge oats", 14, 2240),
        new(3, "Wholemeal bread", 12, 1560),
        new(4, "Cheddar cheese", 9, 1125),
        new(5, "Banana", 11, 990),
    ];

    private static readonly Dictionary<int, int> NovemberKcal = new Dictionary<int, int>
    {
        [1] = 2090,
        [2] = 2090,
        [3] = 2090,
        [4] = 2090,
        [6] = 2090,
        [7] = 2450,
        [8] = 1800,
        [9] = 2090,
        [10] = 2090,
        [11] = 1800,
        [12] = 2090,
        [13] = 2450,
        [14] = 2450,
        [15] = 1800,
        [16] = 2090,
        [17] = 2090,
        [18] = 940,
    };

    private static readonly int[] MonthAverages = [2120, 2090, 2110, 2075, 2105, 2140, 2085, 2230, 2060, 1990];

    /// <summary>The week of 9 to 15 Nov 2026.</summary>
    public static TrendsReport Week { get; } = Build(Enumerable.Range(0, 7).Select(i => Day(new DateOnly(2026, 11, 9).AddDays(i), FixtureWeek[i])).ToList(), []);

    /// <summary>The week of 16 to 22 Nov: two logged days so far, which is too few for a figure.</summary>
    public static TrendsReport LittleData { get; } = Build(
        Enumerable.Range(0, 7).Select(i => Day(new DateOnly(2026, 11, 16).AddDays(i), i switch { 0 => 2210, 1 => 1980, _ => null })).ToList(), []);

    /// <summary>The week of 2 to 8 Nov with nothing logged.</summary>
    public static TrendsReport NoData { get; } = Build(Enumerable.Range(0, 7).Select(i => Day(new DateOnly(2026, 11, 2).AddDays(i), null)).ToList(), []);

    /// <summary>November 2026: 16 of 17 past days logged (the 5th is not), 10 of them on target.</summary>
    public static TrendsReport Month { get; } = Build(NovemberDays(), TopFoods);

    /// <summary>The year 2026 up to today: 283 of 321 past days logged, each month on target and October the lowest.</summary>
    public static TrendsReport Year { get; } = Build(YearDays(), []);

    private static List<TrendDay> NovemberDays() =>
        Enumerable.Range(1, 30).Select(d => Day(new DateOnly(2026, 11, d), NovemberKcal.TryGetValue(d, out var kcal) ? kcal : null)).ToList();

    private static List<TrendDay> YearDays()
    {
        var days = new List<TrendDay>();
        for (var month = 1; month <= 10; month++)
        {
            var unlogged = month <= 7 ? 4 : 3;
            for (var d = 1; d <= DateTime.DaysInMonth(2026, month); d++)
            {
                // The unlogged days fall on the 3rd, 11th, 19th and 27th.
                var missed = d % 8 == 3 && d / 8 < unlogged;
                days.Add(Day(new DateOnly(2026, month, d), missed ? null : MonthAverages[month - 1]));
            }
        }

        days.AddRange(NovemberDays());
        days.AddRange(Enumerable.Range(1, 31).Select(d => Day(new DateOnly(2026, 12, d), null)));
        return days;
    }

    private static TrendDay Day(DateOnly date, int? kcal)
    {
        var logged = kcal is not null;
        var total = kcal ?? 0;
        var macros = logged ? new Nutrition(total, Math.Round(total * 0.086, 1), Math.Round(total * 0.172, 1), Math.Round(total * 0.043, 1)) : Nutrition.Zero;
        var meals = logged ? new MealKcal((int)(total * 0.2), (int)(total * 0.29), (int)(total * 0.37), total - (int)(total * 0.2) - (int)(total * 0.29) - (int)(total * 0.37)) : MealKcal.Zero;
        return new TrendDay(date, Target, macros, meals, logged, StatusOf(date, kcal));
    }

    private static DayStatus StatusOf(DateOnly date, int? kcal) =>
        date > Today ? DayStatus.Future
        : date == Today ? DayStatus.Today
        : kcal is not { } value ? DayStatus.NotLogged
        : 10L * value < 9L * Target ? DayStatus.Under
        : 10L * value > 11L * Target ? DayStatus.Over
        : DayStatus.OnTarget;

    private static TrendsReport Build(List<TrendDay> days, IReadOnlyList<TopFood> topFoods)
    {
        var past = days.Where(d => d.Date < Today).ToList();
        var logged = past.Where(d => d.IsLogged).ToList();
        var enough = logged.Count >= TrendsService.MinDays;
        int Mean(Func<TrendDay, long> pick) => logged.Count == 0 ? 0 : (int)Math.Round((double)logged.Sum(pick) / logged.Count, MidpointRounding.AwayFromZero);
        double MeanGrams(Func<Nutrition, double> pick) => logged.Count == 0 ? 0 : Math.Round(logged.Sum(d => pick(d.Totals)) / logged.Count, 1);
        var average = logged.Count == 0 ? (int?)null : Mean(d => d.Totals.Kcal);
        var range = new TrendsRange(
            past.Count,
            logged.Count,
            average,
            logged.Count == 0 ? null : new Nutrition(average!.Value, MeanGrams(n => n.ProteinG), MeanGrams(n => n.CarbsG), MeanGrams(n => n.FatG)),
            logged.Count == 0 ? null : new MealKcal(Mean(d => d.KcalByMeal.Breakfast), Mean(d => d.KcalByMeal.Lunch), Mean(d => d.KcalByMeal.Dinner), Mean(d => d.KcalByMeal.Snacks)),
            past.Count(d => d.Status == DayStatus.Under),
            past.Count(d => d.Status == DayStatus.OnTarget),
            past.Count(d => d.Status == DayStatus.Over),
            enough ? topFoods : [],
            Streak(days),
            new TrendsEnoughData(enough, enough, enough, logged.Count >= TrendsService.MinDays, enough));

        var length = days.Count;
        var kind = length <= 7 ? TrendBucketKind.None : length <= 62 ? TrendBucketKind.Week : TrendBucketKind.Month;
        var buckets = kind == TrendBucketKind.None
            ? []
            : days.GroupBy(d => kind == TrendBucketKind.Week ? WeekStart(d.Date) : new DateOnly(d.Date.Year, d.Date.Month, 1))
                .Select(g =>
                {
                    var inBucket = g.Where(d => d.Date < Today && d.IsLogged).ToList();
                    return new TrendBucket(
                        g.First().Date,
                        g.Last().Date,
                        inBucket.Count == 0 ? null : (int)Math.Round(inBucket.Average(d => d.Totals.Kcal), MidpointRounding.AwayFromZero),
                        inBucket.Count,
                        g.Count(d => d.Status == DayStatus.Over));
                })
                .ToList();
        return new TrendsReport(days, range, kind, buckets);
    }

    private static int Streak(List<TrendDay> days)
    {
        var streak = 0;
        foreach (var day in days.Where(d => d.Date <= Today).OrderByDescending(d => d.Date))
        {
            if (!day.IsLogged)
            {
                // An empty today does not end the streak; any earlier empty day does.
                if (day.Date == Today)
                {
                    continue;
                }

                break;
            }

            streak++;
        }

        return streak;
    }

    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
