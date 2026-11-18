using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Marks foods as favourites, unmarks them and lists them. A favourite is the food only, with no amount, and the seed clean-up never deletes one.
/// A failing database gives <c>Failed</c>, never an exception. Logs carry food ids and counts only.
/// </summary>
public sealed partial class FavouriteService
{
    private readonly FavouriteStore store;
    private readonly TimeProvider time;
    private readonly ILogger<FavouriteService> logger;

    internal FavouriteService(FavouriteStore store, TimeProvider time, ILogger<FavouriteService> logger)
    {
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Idempotent: a food that is a favourite already gives <see cref="FavouriteStatus.AlreadyFavourite"/> and keeps its one row.</summary>
    public async Task<FavouriteResult> AddAsync(int foodId)
    {
        try
        {
            switch (await store.AddAsync(foodId, time.GetUtcNow()))
            {
                case AddFavouriteOutcome.FoodNotFound:
                    Log.FoodNotFound(logger, foodId);
                    return new FavouriteResult(FavouriteStatus.FoodNotFound);
                case AddFavouriteOutcome.AlreadyFavourite:
                    Log.AlreadyFavourite(logger, foodId);
                    return new FavouriteResult(FavouriteStatus.AlreadyFavourite);
                default:
                    Log.Added(logger, foodId);
                    return new FavouriteResult(FavouriteStatus.Added);
            }
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return new FavouriteResult(FavouriteStatus.Failed);
        }
    }

    /// <summary>Idempotent: a food that is not a favourite gives <see cref="FavouriteStatus.NotFavourite"/>.</summary>
    public async Task<FavouriteResult> RemoveAsync(int foodId)
    {
        try
        {
            if (!await store.RemoveAsync(foodId))
            {
                Log.NotFavourite(logger, foodId);
                return new FavouriteResult(FavouriteStatus.NotFavourite);
            }

            Log.Removed(logger, foodId);
            return new FavouriteResult(FavouriteStatus.Removed);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return new FavouriteResult(FavouriteStatus.Failed);
        }
    }

    public async Task<FavouriteCheckResult> IsFavouriteAsync(int foodId)
    {
        try
        {
            return FavouriteCheckResult.Found(await store.IsFavouriteAsync(foodId));
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return FavouriteCheckResult.Failed();
        }
    }

    public async Task<FavouriteIdsResult> IdsAsync()
    {
        try
        {
            var ids = await store.IdsAsync();
            Log.Listed(logger, ids.Count);
            return FavouriteIdsResult.Found(ids);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return FavouriteIdsResult.Failed();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FavouriteAdded, LogLevel.Debug, "Food {FoodId} marked as a favourite")]
        public static partial void Added(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FavouriteAlreadyFavourite, LogLevel.Debug, "Food {FoodId} is a favourite already")]
        public static partial void AlreadyFavourite(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FavouriteFoodNotFound, LogLevel.Debug, "Favourite not added, no food {FoodId}")]
        public static partial void FoodNotFound(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FavouriteRemoved, LogLevel.Debug, "Food {FoodId} unmarked as a favourite")]
        public static partial void Removed(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FavouriteNotFavourite, LogLevel.Debug, "Food {FoodId} was not a favourite")]
        public static partial void NotFavourite(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FavouriteListed, LogLevel.Debug, "Favourites read, count {Count}")]
        public static partial void Listed(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.FavouriteFailed, LogLevel.Warning, "Favourites database call failed")]
        public static partial void Failed(ILogger logger);
    }
}
