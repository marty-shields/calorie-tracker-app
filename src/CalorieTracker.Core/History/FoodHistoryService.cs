using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.History;

/// <summary>
/// Says which foods the person logged lately, which they usually log at a meal, and how much they last had, from the kept diary entries. Removed entries count for nothing.
/// A failing database gives <see cref="FoodHistoryStatus.Failed"/>, never an exception. Logs carry counts only, never a food, meal, date or amount.
/// </summary>
public sealed partial class FoodHistoryService
{
    /// <summary>How many days back usual foods look, counting today.</summary>
    public const int UsualDays = 30;

    public const int DefaultLimit = 20;
    public const int MaxLimit = 200;

    private readonly FoodHistoryStore store;
    private readonly TimeProvider time;
    private readonly ILogger<FoodHistoryService> logger;

    internal FoodHistoryService(FoodHistoryStore store, TimeProvider time, ILogger<FoodHistoryService> logger)
    {
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Each food once, by its newest entry (the highest entry id), newest first. TimesLogged counts all its kept entries, on any day.</summary>
    /// <param name="limit">Most foods to return; kept between 1 and <see cref="MaxLimit"/>.</param>
    /// <param name="meal">When set, each food's last values are those of its newest entry in that meal, or of its newest entry in any meal when it was never logged in that one.</param>
    public Task<FoodHistoryResult> RecentAsync(int limit = DefaultLimit, Meal? meal = null) =>
        ReadAsync(() => store.RecentAsync(Math.Clamp(limit, 1, MaxLimit), meal), Log.Recent);

    /// <summary>
    /// Foods logged in <paramref name="meal"/> in the last <see cref="UsualDays"/> days (local time, today included), the food logged on most days first, then the one with the newest entry in that meal.
    /// TimesLogged is the number of days. An entry older than that does not count, but the food can still be in <see cref="RecentAsync"/>. The last values are those of the food's newest entry in that meal, whatever its day.
    /// </summary>
    /// <param name="limit">Most foods to return; kept between 1 and <see cref="MaxLimit"/>.</param>
    public Task<FoodHistoryResult> UsualAsync(Meal meal, int limit = DefaultLimit)
    {
        var since = DiaryEntry.FormatDate(DateOnly.FromDateTime(time.GetLocalNow().DateTime).AddDays(1 - UsualDays));
        return ReadAsync(() => store.UsualAsync(meal, since, Math.Clamp(limit, 1, MaxLimit)), Log.Usual);
    }

    /// <summary>For ranking a whole search: the history of every food ever logged, in no set order, in one read. TimesLogged counts all its kept entries.</summary>
    /// <param name="meal">When set, each food's last values are those of its newest entry in that meal, or of its newest entry in any meal when it was never logged in that one.</param>
    public Task<FoodHistoryResult> AllAsync(Meal? meal = null) =>
        ReadAsync(() => store.AllAsync(meal), Log.ForFoods);

    /// <summary>The history of just <paramref name="foodIds"/>, in no set order; a food never logged, or an unknown id, is left out. TimesLogged counts all its kept entries. No ids is an empty result without a database call.</summary>
    /// <param name="meal">When set, each food's last values are those of its newest entry in that meal, or of its newest entry in any meal when it was never logged in that one.</param>
    public Task<FoodHistoryResult> ForFoodsAsync(IReadOnlyCollection<int> foodIds, Meal? meal = null) =>
        foodIds.Count == 0
            ? Task.FromResult(FoodHistoryResult.Found([]))
            : ReadAsync(() => store.ForFoodsAsync(foodIds, meal), Log.ByIds);

    private async Task<FoodHistoryResult> ReadAsync(Func<Task<List<FoodHistoryRow>>> read, Action<ILogger, int> logged)
    {
        try
        {
            var foods = (await read()).Select(FoodHistory.From).ToList();
            logged(logger, foods.Count);
            return FoodHistoryResult.Found(foods);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return FoodHistoryResult.Failed();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodHistoryRecent, LogLevel.Debug, "Recent foods read, count {Count}")]
        public static partial void Recent(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.FoodHistoryUsual, LogLevel.Debug, "Usual foods read, count {Count}")]
        public static partial void Usual(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.FoodHistoryForFoods, LogLevel.Debug, "Food history read, count {Count}")]
        public static partial void ForFoods(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.FoodHistoryByIds, LogLevel.Debug, "Food history read for chosen foods, count {Count}")]
        public static partial void ByIds(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.FoodHistoryFailed, LogLevel.Warning, "Food history read failed")]
        public static partial void Failed(ILogger logger);
    }
}
