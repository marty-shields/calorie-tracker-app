using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.History;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Components.Layout;
using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;
using Shields.UI.Components.Feedback;

namespace CalorieTracker.UI.Screens;

/// <summary>
/// What the Add sheet needs from the services, for every page whose screen has the bar: the usual foods of the chosen meal and adding one in a tap.
/// Logs food ids, meals, counts and statuses only, never a food's name or amount, under the logger of the page that made it.
/// </summary>
internal sealed partial class UsualFoodsFeed(
    FoodHistoryService history,
    FoodService foods,
    DiaryService diary,
    SnackbarService snackbar,
    ILogger logger,
    Action changed) : IDisposable
{
    private const int Rows = 3;

    private int read;
    private bool adding;
    private bool disposed;

    /// <summary>The rows for the sheet. Null while reading, and after a failed read, which hides the section.</summary>
    public IReadOnlyList<UsualFoodRow>? Usual { get; private set; }

    /// <summary>What failed when the last add did not work; null otherwise.</summary>
    public string? AddError { get; private set; }

    public void Dispose() => disposed = true;

    /// <summary>Reads the foods usual for <paramref name="meal"/>: up to 3 rows. The rows are cleared while reading; a read that lands after a newer one is dropped.</summary>
    public async Task LoadAsync(Meal meal)
    {
        var current = ++read;
        Usual = null;
        AddError = null;
        var result = await history.UsualAsync(meal, Rows);
        if (current != read || disposed)
        {
            return;
        }

        if (result.Status != FoodHistoryStatus.Found)
        {
            Log.ReadFailed(logger, meal);
            return;
        }

        var rows = new List<UsualFoodRow>();
        foreach (var entry in result.Foods)
        {
            var found = await foods.GetAsync(entry.FoodId);
            if (current != read || disposed)
            {
                return;
            }

            if (found is { Status: FoodStatus.Found, Food: { } food })
            {
                rows.Add(UsualFoodRow.From(food, found.Servings, entry));
            }
        }

        Log.Read(logger, meal, rows.Count);
        Usual = rows;
    }

    /// <summary>Adds the usual food to the meal and day with its last amount, servings and serving, and posts "{title} added to {meal}" with Undo. False keeps the sheet open, with <see cref="AddError"/> saying what failed.</summary>
    public async Task<bool> AddAsync(UsualAddRequest request, DateOnly day)
    {
        if (adding)
        {
            return false;
        }

        adding = true;
        try
        {
            var result = await diary.AddAsync(day, request.Meal, request.Food.FoodId, request.Food.LastAmount, request.Food.LastServings, request.Food.LastServingId);
            if (result.Status != DiaryStatus.Ok || result.Entry is not { } entry)
            {
                Log.AddFailed(logger, request.Food.FoodId, request.Meal, result.Status);
                AddError = $"Couldn't add {request.Food.Title} to {request.Meal}. Try again.";
                changed();
                return false;
            }

            Log.Added(logger, request.Food.FoodId, request.Meal);
            AddError = null;
            var (service, bar, log) = (diary, snackbar, logger);
            snackbar.Show($"{request.Food.Title} added to {request.Meal}", "Undo", () => AddUndo.RunAsync(service, bar, status => Log.UndoFailed(log, status), entry.Id));
            return true;
        }
        finally
        {
            adding = false;
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.UsualRead, LogLevel.Debug, "Usual foods for the Add sheet read for {Meal}, rows {Count}")]
        public static partial void Read(ILogger logger, Meal meal, int count);

        [LoggerMessage((int)UiEvent.UsualReadFailed, LogLevel.Warning, "Usual foods for the Add sheet could not be read for {Meal}")]
        public static partial void ReadFailed(ILogger logger, Meal meal);

        [LoggerMessage((int)UiEvent.UsualAdded, LogLevel.Information, "Usual food {FoodId} added to {Meal}")]
        public static partial void Added(ILogger logger, int foodId, Meal meal);

        [LoggerMessage((int)UiEvent.UsualAddFailed, LogLevel.Warning, "Usual food {FoodId} could not be added to {Meal}: {Status}")]
        public static partial void AddFailed(ILogger logger, int foodId, Meal meal, DiaryStatus status);

        [LoggerMessage((int)UiEvent.UsualUndoFailed, LogLevel.Warning, "Undo could not remove the usual food just added: {Status}")]
        public static partial void UndoFailed(ILogger logger, DiaryStatus status);
    }
}
