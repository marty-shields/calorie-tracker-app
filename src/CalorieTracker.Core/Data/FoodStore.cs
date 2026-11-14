using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes <see cref="Food"/> rows, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed partial class FoodStore(SQLiteAsyncConnection connection, DatabaseGate gate, ILogger<FoodStore> logger)
{
    /// <summary>Finds a food by barcode. The barcode index is not unique, so when rows share one the lowest id (the oldest row) wins.</summary>
    public Task<Food?> FindByBarcodeAsync(string barcode) =>
        gate.RunAsync<Food?>(async () =>
            await connection.Table<Food>().Where(f => f.Barcode == barcode).OrderBy(f => f.Id).FirstOrDefaultAsync());

    /// <summary>Inserts the food, or updates the row with the same source and source id. Returns the stored food with its id.</summary>
    public Task<Food> UpsertAsync(Food food) =>
        gate.RunAsync(async () =>
        {
            var inserted = false;
            await connection.RunInTransactionAsync(db =>
            {
                var existing = db.Table<Food>().FirstOrDefault(f => f.Source == food.Source && f.SourceId == food.SourceId);
                if (existing is null)
                {
                    db.Insert(food);
                    inserted = true;
                }
                else
                {
                    food.Id = existing.Id;
                    db.Update(food);
                }
            });

            if (inserted)
            {
                Log.Inserted(logger, food.Id);
            }
            else
            {
                Log.Updated(logger, food.Id);
            }

            return food;
        });

    /// <summary>The seed version last fully loaded for <paramref name="source"/>, or null when none was.</summary>
    public Task<int?> GetSeedVersionAsync(FoodSource source) =>
        gate.RunAsync<int?>(async () =>
            (await connection.Table<SeedVersion>().Where(v => v.Source == source).FirstOrDefaultAsync())?.Version);

    public Task SetSeedVersionAsync(FoodSource source, int version) =>
        gate.RunAsync(async () =>
            await connection.InsertOrReplaceAsync(new SeedVersion { Source = source, Version = version }));

    /// <summary>
    /// Inserts or updates the foods in one transaction, matching rows by source and source id.
    /// Every food must have the same <see cref="Food.Source"/> and a distinct source id; rows of other sources are not touched.
    /// </summary>
    public Task<UpsertManyResult> UpsertManyAsync(IReadOnlyList<Food> foods) =>
        gate.RunAsync(async () =>
        {
            var result = new UpsertManyResult(0, 0);
            if (foods.Count == 0)
            {
                return result;
            }

            var source = foods[0].Source;
            await connection.RunInTransactionAsync(db =>
            {
                var ids = db.Table<Food>().Where(f => f.Source == source).ToList().ToDictionary(f => f.SourceId, f => f.Id);
                var toInsert = new List<Food>();
                var toUpdate = new List<Food>();
                foreach (var food in foods)
                {
                    if (ids.TryGetValue(food.SourceId, out var id))
                    {
                        food.Id = id;
                        toUpdate.Add(food);
                    }
                    else
                    {
                        toInsert.Add(food);
                    }
                }

                db.InsertAll(toInsert);
                db.UpdateAll(toUpdate);
                result = new UpsertManyResult(toInsert.Count, toUpdate.Count);
            });

            return result;
        });

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodInserted, LogLevel.Debug, "Food {FoodId} inserted")]
        public static partial void Inserted(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodUpdated, LogLevel.Debug, "Food {FoodId} updated")]
        public static partial void Updated(ILogger logger, int foodId);
    }
}

/// <summary>How many foods <see cref="FoodStore.UpsertManyAsync"/> inserted and updated.</summary>
internal readonly record struct UpsertManyResult(int Inserted, int Updated);
