using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes <see cref="Food"/> rows, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed partial class FoodStore(SQLiteAsyncConnection connection, DatabaseGate gate, ILogger<FoodStore> logger)
{
    /// <summary>Finds the food a barcode is linked to, or null when it is linked to none. Its <see cref="Food.ServingLabel"/> and <see cref="Food.ServingAmount"/> are those of its Product serving row (null without one).</summary>
    public Task<Food?> FindByBarcodeAsync(string barcode) =>
        gate.RunAsync<Food?>(async () =>
        {
            Food? found = null;
            await connection.RunInTransactionAsync(db =>
            {
                found = db.FindWithQuery<Food>("SELECT f.* FROM Food f JOIN FoodBarcode b ON b.FoodId = f.Id WHERE b.Barcode = ?", barcode);
                if (found is not null)
                {
                    SetProductServing(db, found);
                }
            });

            return found;
        });

    /// <summary>The food with this id as stored, or null when there is none. Its <see cref="Food.ServingLabel"/> and <see cref="Food.ServingAmount"/> are those of its Product serving row (null without one).</summary>
    public Task<Food?> GetAsync(int id) =>
        gate.RunAsync<Food?>(async () =>
        {
            Food? found = null;
            await connection.RunInTransactionAsync(db =>
            {
                found = db.Find<Food>(id);
                if (found is not null)
                {
                    SetProductServing(db, found);
                }
            });

            return found;
        });

    /// <summary>The food with this id and its servings in listing order (see <see cref="Serving.SortOrder"/>), read together; null food when there is none.</summary>
    public Task<(Food? Food, List<Serving> Servings)> GetWithServingsAsync(int id) =>
        gate.RunAsync(async () =>
        {
            Food? found = null;
            var servings = new List<Serving>();
            await connection.RunInTransactionAsync(db =>
            {
                found = db.Find<Food>(id);
                if (found is not null)
                {
                    SetProductServing(db, found);
                    servings = ServingsOf(db, id);
                }
            });

            return (found, servings);
        });

    /// <summary>The servings of a food, product first, then packs, then the person's own, each by amount. Runs on the caller's connection, inside the gate.</summary>
    internal static List<Serving> ServingsOf(SQLiteConnection db, int foodId) =>
        db.Query<Serving>("SELECT * FROM Serving WHERE FoodId = ? ORDER BY SortOrder, Amount, Id", foodId);

    /// <summary>
    /// Sets the food's transient <see cref="Food.ServingLabel"/> and <see cref="Food.ServingAmount"/> from its Product serving row, or to null when it has none, so the old Food columns are never read.
    /// </summary>
    private static void SetProductServing(SQLiteConnection db, Food food)
    {
        var product = db.FindWithQuery<Serving>("SELECT * FROM Serving WHERE FoodId = ? AND Source = ?", food.Id, (int)ServingSource.Product);
        food.ServingLabel = product?.Label;
        food.ServingAmount = product?.Amount;
    }

    /// <summary>
    /// Writes the serving rows a looked-up food carries (see <see cref="Foods.ServingRows"/>): the Product row is added or, when the food has one already, changed in place so its id stays; the Pack row is added unless its amount is stored already.
    /// A food with neither serving part and <see cref="Food.ServingChecked"/> true (the source flags its serving as wrong) loses its Product row, and the entries that used it keep their amounts with no serving link. Rows the person added are never touched.
    /// </summary>
    private static void WriteServings(SQLiteConnection db, int foodId, Food fresh)
    {
        var rows = Foods.ServingRows.Of(fresh);
        var product = rows.FirstOrDefault(r => r.Source == ServingSource.Product);
        if (product is not null)
        {
            var existing = db.FindWithQuery<Serving>("SELECT * FROM Serving WHERE FoodId = ? AND Source = ?", foodId, (int)ServingSource.Product);
            if (existing is null)
            {
                product.FoodId = foodId;
                db.Insert(product);
            }
            else if (existing.Label != product.Label || existing.Amount != product.Amount)
            {
                db.Execute("UPDATE Serving SET Label = ?, Amount = ? WHERE Id = ?", product.Label, product.Amount, existing.Id);
            }
        }
        else if (fresh.ServingChecked is true)
        {
            DeleteServings(db, "FoodId = ? AND Source = ?", foodId, (int)ServingSource.Product);
        }

        foreach (var pack in rows.Where(r => r.Source == ServingSource.Pack))
        {
            db.Execute(
                "INSERT OR IGNORE INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (?, ?, ?, ?, ?)",
                foodId, pack.Label, pack.Amount, (int)ServingSource.Pack, pack.SortOrder);
        }
    }

    /// <summary>Deletes the serving rows matching the condition and clears the link of every diary entry that used them. Runs on the caller's connection, inside the gate.</summary>
    internal static int DeleteServings(SQLiteConnection db, string condition, params object[] args)
    {
        db.Execute($"UPDATE DiaryEntry SET ServingId = NULL WHERE ServingId IN (SELECT Id FROM Serving WHERE {condition})", args);
        return db.Execute($"DELETE FROM Serving WHERE {condition}", args);
    }

    /// <summary>The foods with these ids, in no set order; an id with no food is left out.</summary>
    public Task<List<Food>> GetManyAsync(IReadOnlyCollection<int> ids) =>
        gate.RunAsync(async () =>
        {
            var found = new List<Food>();
            await connection.RunInTransactionAsync(db => found = FindIn(db, ids));
            return found;
        });

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
    /// The food's serving rows are written with it (see <see cref="WriteServings"/>); a lookup with no serving leaves the stored ones as they are.
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

                WriteServings(db, food.Id, food);
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
    /// The serving rows follow <see cref="WriteServings"/>: the product's serving and pack are written, a serving the source flags as wrong is removed and the food marked checked, and rows the person added stay. The food id and the diary entries are untouched.
    /// False when no food has that id.
    /// </summary>
    public Task<bool> RefreshAsync(int id, Food fresh) =>
        gate.RunAsync(async () =>
        {
            var changed = false;
            await connection.RunInTransactionAsync(db =>
            {
                var checkedSet = fresh.ServingChecked is true ? "ServingChecked = 1, " : "";
                changed = db.Execute(
                    $"UPDATE Food SET {checkedSet}Kcal = ?, ProteinG = ?, CarbsG = ?, FatG = ?, FibreG = ?, SugarsG = ?, SaturatesG = ?, SaltG = ?, Brands = ?, GenericName = ?, PackageLabel = ?, PackageAmount = ?, Category = ?, Popularity = ?, Completeness = ?, SourceUpdated = ?, FetchedAt = ?, SearchText = COALESCE(NULLIF(?, ''), SearchText) WHERE Id = ?",
                    fresh.Kcal, fresh.ProteinG, fresh.CarbsG, fresh.FatG, fresh.FibreG, fresh.SugarsG, fresh.SaturatesG, fresh.SaltG,
                    fresh.Brands, fresh.GenericName, fresh.PackageLabel, fresh.PackageAmount, fresh.Category, fresh.Popularity, fresh.Completeness, fresh.SourceUpdated?.UtcTicks, fresh.FetchedAt?.UtcTicks, fresh.SearchText, id) > 0;
                if (changed)
                {
                    WriteServings(db, id, fresh);
                }
            });

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
    /// Deletes the foods of <paramref name="source"/> whose source id is not in <paramref name="keepSourceIds"/>, with their barcode links and servings, in one transaction. Rows of other sources are not touched.
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
                    db.Execute($"DELETE FROM Serving WHERE FoodId IN ({marks})", args);
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
