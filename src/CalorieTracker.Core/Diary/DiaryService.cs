using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Diary;

/// <summary>
/// Adds, changes, removes, restores and lists diary entries through <see cref="DiaryStore"/>. A failing database gives <see cref="DiaryStatus.Failed"/>, never an exception.
/// Logs carry entry ids, food ids and counts only, never a date, amount or food.
/// </summary>
public sealed partial class DiaryService
{
    private readonly DiaryStore store;
    private readonly ILogger<DiaryService> logger;

    internal DiaryService(DiaryStore store, ILogger<DiaryService> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    /// <summary>Raised, possibly on another thread, after an entry is added, changed, removed or restored, so a screen showing a day can read it again.</summary>
    public event Action? Changed;

    /// <summary>The largest amount an entry can hold, in grams or millilitres.</summary>
    public const double MaxAmount = 10_000;

    /// <summary>
    /// Adds an entry of a food. <paramref name="amount"/> (grams or millilitres) must be finite and above zero, up to <see cref="MaxAmount"/>, <paramref name="servings"/> when given the same, and <paramref name="meal"/> a defined meal;
    /// otherwise <see cref="DiaryStatus.Invalid"/>. <see cref="DiaryStatus.NotFound"/> when no food has the id. Nothing is added unless Ok.
    /// </summary>
    public async Task<DiaryResult> AddAsync(DateOnly date, Meal meal, int foodId, double amount, double? servings = null)
    {
        if (!IsValid(meal, amount, servings))
        {
            Log.Rejected(logger);
            return DiaryResult.Invalid();
        }

        try
        {
            var added = await store.AddAsync(new DiaryEntry { Date = DiaryEntry.FormatDate(date), Meal = meal, FoodId = foodId, Amount = amount, Servings = servings });
            if (added is null)
            {
                Log.FoodNotFound(logger, foodId);
                return DiaryResult.NotFound();
            }

            Log.Added(logger, added.Id, foodId);
            Changed?.Invoke();
            return DiaryResult.Ok(added);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return DiaryResult.Failed();
        }
    }

    /// <summary>
    /// Changes the day, meal, amount and servings of a kept entry; its food and place in the order added stay. Validated like <see cref="AddAsync"/>.
    /// <see cref="DiaryStatus.NotFound"/> when no kept entry has the id (a removed one must be restored first).
    /// </summary>
    public async Task<DiaryResult> UpdateAsync(int id, DateOnly date, Meal meal, double amount, double? servings = null)
    {
        if (!IsValid(meal, amount, servings))
        {
            Log.Rejected(logger);
            return DiaryResult.Invalid();
        }

        return await ChangeAsync(id, () => store.UpdateAsync(id, DiaryEntry.FormatDate(date), meal, amount, servings), Log.Updated);
    }

    /// <summary>Removes a kept entry so <see cref="RestoreAsync"/> can bring it back. <see cref="DiaryStatus.NotFound"/> when no kept entry has the id.</summary>
    public Task<DiaryResult> RemoveAsync(int id) => ChangeAsync(id, () => store.SetRemovedAsync(id, true), Log.Removed);

    /// <summary>Brings a removed entry back to its place in its day. <see cref="DiaryStatus.NotFound"/> when no removed entry has the id.</summary>
    public Task<DiaryResult> RestoreAsync(int id) => ChangeAsync(id, () => store.SetRemovedAsync(id, false), Log.Restored);

    /// <summary>The kept entries of the day, in the order added; removed entries are left out.</summary>
    public async Task<DiaryResult> ListAsync(DateOnly date)
    {
        try
        {
            var entries = await store.ListAsync(DiaryEntry.FormatDate(date));
            Log.Listed(logger, entries.Count);
            return DiaryResult.Listed(entries);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return DiaryResult.Failed();
        }
    }

    private async Task<DiaryResult> ChangeAsync(int id, Func<Task<bool>> change, Action<ILogger, int> logged)
    {
        try
        {
            if (!await change())
            {
                Log.EntryNotFound(logger, id);
                return DiaryResult.NotFound();
            }

            logged(logger, id);
            Changed?.Invoke();
            return DiaryResult.Ok();
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return DiaryResult.Failed();
        }
    }

    private static bool IsValid(Meal meal, double amount, double? servings) =>
        Enum.IsDefined(meal) && IsPositive(amount) && amount <= MaxAmount && (servings is null || IsPositive(servings.Value));

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.DiaryEntryAdded, LogLevel.Debug, "Diary entry {EntryId} added for food {FoodId}")]
        public static partial void Added(ILogger logger, int entryId, int foodId);

        [LoggerMessage((int)CoreEvent.DiaryEntryUpdated, LogLevel.Debug, "Diary entry {EntryId} updated")]
        public static partial void Updated(ILogger logger, int entryId);

        [LoggerMessage((int)CoreEvent.DiaryEntryRemoved, LogLevel.Debug, "Diary entry {EntryId} removed")]
        public static partial void Removed(ILogger logger, int entryId);

        [LoggerMessage((int)CoreEvent.DiaryEntryRestored, LogLevel.Debug, "Diary entry {EntryId} restored")]
        public static partial void Restored(ILogger logger, int entryId);

        [LoggerMessage((int)CoreEvent.DiaryListed, LogLevel.Debug, "Diary listed, {Count} entries")]
        public static partial void Listed(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.DiaryRejected, LogLevel.Debug, "Diary entry values rejected")]
        public static partial void Rejected(ILogger logger);

        [LoggerMessage((int)CoreEvent.DiaryEntryNotFound, LogLevel.Debug, "Diary entry {EntryId} not found in the needed state")]
        public static partial void EntryNotFound(ILogger logger, int entryId);

        [LoggerMessage((int)CoreEvent.DiaryFoodNotFound, LogLevel.Debug, "Diary entry not added, food {FoodId} not found")]
        public static partial void FoodNotFound(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.DiaryFailed, LogLevel.Warning, "Diary call failed")]
        public static partial void Failed(ILogger logger);
    }
}
