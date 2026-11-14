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

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodInserted, LogLevel.Debug, "Food {FoodId} inserted")]
        public static partial void Inserted(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodUpdated, LogLevel.Debug, "Food {FoodId} updated")]
        public static partial void Updated(ILogger logger, int foodId);
    }
}
