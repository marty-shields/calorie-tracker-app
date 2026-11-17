using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Adds and deletes the servings a person makes for a food. Only User servings are deleted; the ones that came with the product stay. A failing database gives <see cref="ServingStatus.Failed"/>, never an exception.
/// A serving's label is what a person typed, so logs carry serving ids and food ids only, never a label or amount.
/// </summary>
public sealed partial class ServingService
{
    private readonly ServingStore store;
    private readonly ILogger<ServingService> logger;

    internal ServingService(ServingStore store, ILogger<ServingService> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    /// <summary>
    /// Adds a serving of the person's own. The label is trimmed and must pass <see cref="FoodService.TryBoundServing"/> with the amount, otherwise <see cref="ServingStatus.Invalid"/>.
    /// <see cref="ServingStatus.Duplicate"/> when the food has a serving of the person's own with that amount; <see cref="ServingStatus.NotFound"/> when no food has the id.
    /// </summary>
    public async Task<ServingResult> AddAsync(int foodId, string? label, double amount)
    {
        if (!FoodService.TryBoundServing(label, amount, out var trimmed, out var bounded))
        {
            Log.Rejected(logger);
            return ServingResult.Invalid();
        }

        try
        {
            var (outcome, serving) = await store.AddUserAsync(foodId, trimmed, bounded);
            switch (outcome)
            {
                case AddUserServingOutcome.FoodNotFound:
                    Log.FoodNotFound(logger, foodId);
                    return ServingResult.NotFound();
                case AddUserServingOutcome.Duplicate:
                    Log.Duplicate(logger, foodId);
                    return ServingResult.Duplicate();
                default:
                    Log.Added(logger, serving!.Id, foodId);
                    return ServingResult.Ok(serving);
            }
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return ServingResult.Failed();
        }
    }

    /// <summary>
    /// Deletes a serving of the person's own. Diary entries that used it keep their amount, servings count and calories and lose only the link to it, so a diary row then shows the amount instead of the serving's label.
    /// <see cref="ServingStatus.NotFound"/> when no User serving has the id.
    /// </summary>
    public async Task<ServingResult> RemoveAsync(int servingId)
    {
        try
        {
            if (!await store.RemoveUserAsync(servingId))
            {
                Log.NotFound(logger, servingId);
                return ServingResult.NotFound();
            }

            Log.Removed(logger, servingId);
            return ServingResult.Ok();
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return ServingResult.Failed();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.ServingAdded, LogLevel.Debug, "Serving {ServingId} added for food {FoodId}")]
        public static partial void Added(ILogger logger, int servingId, int foodId);

        [LoggerMessage((int)CoreEvent.ServingRemoved, LogLevel.Debug, "Serving {ServingId} removed")]
        public static partial void Removed(ILogger logger, int servingId);

        [LoggerMessage((int)CoreEvent.ServingRejected, LogLevel.Debug, "Serving values rejected")]
        public static partial void Rejected(ILogger logger);

        [LoggerMessage((int)CoreEvent.ServingFoodNotFound, LogLevel.Debug, "Serving not added, food {FoodId} not found")]
        public static partial void FoodNotFound(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.ServingDuplicate, LogLevel.Debug, "Serving not added, food {FoodId} has one of that amount")]
        public static partial void Duplicate(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.ServingNotFound, LogLevel.Debug, "Serving {ServingId} not found among the person's own")]
        public static partial void NotFound(ILogger logger, int servingId);

        [LoggerMessage((int)CoreEvent.ServingFailed, LogLevel.Warning, "Serving call failed")]
        public static partial void Failed(ILogger logger);
    }
}
