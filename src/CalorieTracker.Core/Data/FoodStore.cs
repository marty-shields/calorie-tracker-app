using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes <see cref="Food"/> rows, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed partial class FoodStore(SQLiteAsyncConnection connection, DatabaseGate gate, ILogger<FoodStore> logger)
{
    /// <summary>Finds the food a barcode is linked to, or null when it is linked to none.</summary>
    public Task<Food?> FindByBarcodeAsync(string barcode) =>
        gate.RunAsync<Food?>(async () =>
            await connection.FindWithQueryAsync<Food>(
                "SELECT f.* FROM Food f JOIN FoodBarcode b ON b.FoodId = f.Id WHERE b.Barcode = ?", barcode));

    /// <summary>The food with this id as stored, or null when there is none.</summary>
    public Task<Food?> GetAsync(int id) =>
        gate.RunAsync<Food?>(async () => await connection.FindAsync<Food>(id));

    /// <summary>The foods with these ids; an id with no food is left out. Runs on the caller's connection, inside the gate.</summary>
    internal static List<Food> FindIn(SQLiteConnection db, IReadOnlyCollection<int> ids)
    {
        var found = new List<Food>();
        // Chunked so a long list stays under SQLite's limit on bound variables.
        foreach (var chunk in ids.Distinct().Chunk(500))
        {
            var marks = string.Join(",", chunk.Select(_ => "?"));
            found.AddRange(db.Query<Food>($"SELECT * FROM Food WHERE Id IN ({marks})", chunk.Cast<object>().ToArray()));
        }

        return found;
    }

    /// <summary>Every food, in no particular order. Ranking happens in memory, which is fine at the seed's size.</summary>
    public Task<List<Food>> ListAsync(CancellationToken ct = default) =>
        gate.RunAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            return await connection.Table<Food>().ToListAsync();
        });

    /// <summary>
    /// Inserts the food, or updates the row with the same source and source id, and returns the stored food with its id.
    /// With <paramref name="barcode"/>, links it to the food in the same transaction; a barcode already linked to another food stays with that food.
    /// </summary>
    public Task<Food> UpsertAsync(Food food, string? barcode = null) =>
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
                    // A lookup that knows nothing about the serving must not undo a check made earlier.
                    if (food.ServingAmount is null)
                    {
                        food.ServingChecked ??= existing.ServingChecked;
                    }

                    db.Update(food);
                }

                if (barcode is not null)
                {
                    db.Execute("INSERT OR IGNORE INTO FoodBarcode (Barcode, FoodId) VALUES (?, ?)", barcode, food.Id);
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

    /// <summary>
    /// Sets the nutrients (kcal, protein, carbs, fat, fibre, sugars, saturates, salt), the source details (brands, generic name, package, category, popularity, completeness, source update and fetch time) and the search text of the food with <paramref name="id"/> from <paramref name="fresh"/>; every other column stays as stored.
    /// The serving is set (with its label) when <paramref name="fresh"/> has both parts. With neither and <see cref="Food.ServingChecked"/> true on <paramref name="fresh"/> (the source flags the serving as wrong) it is cleared and the food marked checked. Otherwise the stored serving stays.
    /// False when no food has that id.
    /// </summary>
    public Task<bool> RefreshAsync(int id, Food fresh) =>
        gate.RunAsync(async () =>
        {
            var hasServing = fresh is { ServingLabel: not null, ServingAmount: not null };
            var args = new List<object?>();
            var servingSet = "";
            if (hasServing)
            {
                servingSet = "ServingLabel = ?, ServingAmount = ?, ";
                args.Add(fresh.ServingLabel);
                args.Add(fresh.ServingAmount);
            }
            else if (fresh.ServingChecked is true)
            {
                servingSet = "ServingLabel = NULL, ServingAmount = NULL, ServingChecked = 1, ";
            }

            args.AddRange([fresh.Kcal, fresh.ProteinG, fresh.CarbsG, fresh.FatG, fresh.FibreG, fresh.SugarsG, fresh.SaturatesG, fresh.SaltG,
                fresh.Brands, fresh.GenericName, fresh.PackageLabel, fresh.PackageAmount, fresh.Category, fresh.Popularity, fresh.Completeness, fresh.SourceUpdated?.UtcTicks, fresh.FetchedAt?.UtcTicks, fresh.SearchText, id]);
            var changed = await connection.ExecuteAsync(
                $"UPDATE Food SET {servingSet}Kcal = ?, ProteinG = ?, CarbsG = ?, FatG = ?, FibreG = ?, SugarsG = ?, SaturatesG = ?, SaltG = ?, Brands = ?, GenericName = ?, PackageLabel = ?, PackageAmount = ?, Category = ?, Popularity = ?, Completeness = ?, SourceUpdated = ?, FetchedAt = ?, SearchText = COALESCE(NULLIF(?, ''), SearchText) WHERE Id = ?",
                args.ToArray()) > 0;
            if (changed)
            {
                Log.Updated(logger, id);
            }

            return changed;
        });

    /// <summary>Marks the food as asked for a serving, so it is not asked again (see <see cref="Food.ServingChecked"/>). False when no food has that id.</summary>
    public Task<bool> MarkServingCheckedAsync(int id) =>
        gate.RunAsync(async () =>
        {
            var changed = await connection.ExecuteAsync("UPDATE Food SET ServingChecked = 1 WHERE Id = ?", id) > 0;
            if (changed)
            {
                Log.Updated(logger, id);
            }

            return changed;
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
    /// Every food must have the same <see cref="Food.Source"/> (otherwise throws <see cref="ArgumentException"/>) and a distinct source id; rows of other sources are not touched.
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
            if (foods.Any(f => f.Source != source))
            {
                throw new ArgumentException("Every food must have the same source", nameof(foods));
            }

            await connection.RunInTransactionAsync(db =>
            {
                var ids = db.Query<IdRow>("SELECT Id, SourceId FROM Food WHERE Source = ?", (int)source).ToDictionary(r => r.SourceId, r => r.Id);
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

    /// <summary>
    /// Deletes the foods of <paramref name="source"/> whose source id is not in <paramref name="keepSourceIds"/>, with their barcode links, in one transaction. Rows of other sources are not touched.
    /// A food any <see cref="DiaryEntry"/> points at, removed or not, is kept with its barcodes.
    /// </summary>
    /// <returns>How many foods were deleted.</returns>
    public Task<int> DeleteMissingAsync(FoodSource source, IReadOnlySet<string> keepSourceIds) =>
        gate.RunAsync(async () =>
        {
            var deleted = 0;
            await connection.RunInTransactionAsync(db =>
            {
                var stale = db.Query<IdRow>("SELECT Id, SourceId FROM Food WHERE Source = ? AND Id NOT IN (SELECT FoodId FROM DiaryEntry)", (int)source)
                    .Where(r => !keepSourceIds.Contains(r.SourceId))
                    .Select(r => r.Id)
                    .ToList();
                foreach (var chunk in stale.Chunk(500))
                {
                    var marks = string.Join(",", chunk.Select(_ => "?"));
                    var args = chunk.Cast<object>().ToArray();
                    db.Execute($"DELETE FROM FoodBarcode WHERE FoodId IN ({marks})", args);
                    db.Execute($"DELETE FROM Food WHERE Id IN ({marks})", args);
                }

                deleted = stale.Count;
            });

            return deleted;
        });

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodInserted, LogLevel.Debug, "Food {FoodId} inserted")]
        public static partial void Inserted(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodUpdated, LogLevel.Debug, "Food {FoodId} updated")]
        public static partial void Updated(ILogger logger, int foodId);
    }
}

internal sealed class IdRow
{
    public int Id { get; set; }

    public string SourceId { get; set; } = "";
}

/// <summary>How many foods <see cref="FoodStore.UpsertManyAsync"/> inserted and updated.</summary>
internal readonly record struct UpsertManyResult(int Inserted, int Updated);
