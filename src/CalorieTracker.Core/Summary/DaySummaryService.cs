using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Summary;

/// <summary>
/// Builds a day's totals, macro shares, meals, entries and target position from <see cref="DiaryService"/>, <see cref="FoodStore"/> and <see cref="TargetService"/>.
/// Nothing throws: a failing database gives <see cref="DaySummaryStatus.Failed"/>, and an entry whose food is gone gives <see cref="DaySummaryStatus.FoodMissing"/> for the whole day.
/// Logs carry entry ids, food ids and counts only, never a date, amount, name or calories.
/// </summary>
public sealed partial class DaySummaryService
{
    private readonly DiaryService diary;
    private readonly TargetService targets;
    private readonly FoodStore foods;
    private readonly ILogger<DaySummaryService> logger;

    internal DaySummaryService(DiaryService diary, TargetService targets, FoodStore foods, ILogger<DaySummaryService> logger)
    {
        this.diary = diary;
        this.targets = targets;
        this.foods = foods;
        this.logger = logger;
    }

    /// <summary>
    /// The summary of the kept entries of <paramref name="date"/>. Each entry's nutrition is its food's per-100 values scaled by amount over 100 (grams or millilitres alike) and rounded as <see cref="Nutrition"/> says.
    /// Shares come from the unrounded macros so rounding does not tip them.
    /// </summary>
    public async Task<DaySummaryResult> GetAsync(DateOnly date)
    {
        try
        {
            var listed = await diary.ListAsync(date);
            if (listed.Status != DiaryStatus.Ok)
            {
                return DaySummaryResult.Failed(); // DiaryService logged it
            }

            var target = await targets.GetAsync();
            if (target.Status != TargetStatus.Ok)
            {
                return DaySummaryResult.Failed(); // TargetService logged it
            }

            var ids = listed.Entries.Select(e => e.FoodId).Distinct().ToList();
            var found = ids.Count == 0 ? [] : (await foods.FindAsync(ids)).ToDictionary(f => f.Id);

            var byMeal = Enum.GetValues<Meal>().ToDictionary(m => m, _ => new List<SummaryEntry>());
            decimal protein = 0, carbs = 0, fat = 0;
            foreach (var entry in listed.Entries)
            {
                if (!found.TryGetValue(entry.FoodId, out var food))
                {
                    Log.FoodMissing(logger, entry.Id, entry.FoodId);
                    return DaySummaryResult.FoodMissing(entry.Id);
                }

                if (!byMeal.TryGetValue(entry.Meal, out var mealEntries))
                {
                    Log.MealUnknown(logger, entry.Id);
                    return DaySummaryResult.Failed();
                }

                var scale = (decimal)entry.Amount / 100m;
                var p = Nutrition.ToDecimal(food.ProteinG) * scale;
                var c = Nutrition.ToDecimal(food.CarbsG) * scale;
                var f = Nutrition.ToDecimal(food.FatG) * scale;
                protein += p;
                carbs += c;
                fat += f;
                var nutrition = new Nutrition(
                    (int)Math.Round(Nutrition.ToDecimal(food.Kcal) * scale, MidpointRounding.AwayFromZero),
                    Nutrition.Round(p),
                    Nutrition.Round(c),
                    Nutrition.Round(f));
                mealEntries.Add(new SummaryEntry(entry.Id, food.Id, food.Name, food.Brand, food.Unit, entry.Amount, entry.Servings, nutrition));
            }

            var meals = byMeal
                .Select(m => new MealSummary(m.Key, m.Value.Aggregate(Nutrition.Zero, (sum, e) => sum.Plus(e.Nutrition)), m.Value))
                .ToList();
            var totals = meals.Aggregate(Nutrition.Zero, (sum, m) => sum.Plus(m.Totals));
            var summary = new DaySummary(
                totals,
                SharesOf(protein, carbs, fat),
                meals,
                target.Kcal,
                checked(target.Kcal - totals.Kcal),
                target.Kcal is { } goal && totals.Kcal > goal);
            Log.Built(logger, listed.Entries.Count);
            return DaySummaryResult.Ok(summary);
        }
        catch (OverflowException)
        {
            Log.Overflow(logger);
            return DaySummaryResult.Failed();
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return DaySummaryResult.Failed();
        }
    }

    private static MacroShares SharesOf(decimal protein, decimal carbs, decimal fat)
    {
        var (p, c, f) = (protein * 4, carbs * 4, fat * 9);
        var sum = p + c + f;
        return sum == 0
            ? new MacroShares(0, 0, 0)
            : new MacroShares(Percent(p, sum), Percent(c, sum), Percent(f, sum));
    }

    private static int Percent(decimal part, decimal whole) => (int)Math.Round(100 * part / whole, MidpointRounding.AwayFromZero);

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.DaySummaryBuilt, LogLevel.Debug, "Day summary built from {Count} entries")]
        public static partial void Built(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.DaySummaryFoodMissing, LogLevel.Warning, "Day summary not built, entry {EntryId} points at missing food {FoodId}")]
        public static partial void FoodMissing(ILogger logger, int entryId, int foodId);

        [LoggerMessage((int)CoreEvent.DaySummaryOverflow, LogLevel.Warning, "Day summary not built, a figure was too large")]
        public static partial void Overflow(ILogger logger);

        [LoggerMessage((int)CoreEvent.DaySummaryMealUnknown, LogLevel.Warning, "Day summary not built, entry {EntryId} has a meal that does not exist")]
        public static partial void MealUnknown(ILogger logger, int entryId);

        [LoggerMessage((int)CoreEvent.DaySummaryFailed, LogLevel.Warning, "Day summary call failed")]
        public static partial void Failed(ILogger logger);
    }
}
