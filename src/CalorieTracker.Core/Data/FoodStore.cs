using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes <see cref="Food"/> rows, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed partial class FoodStore(SQLiteAsyncConnection connection, DatabaseGate gate, ILogger<FoodStore> logger)
{
    /// <summary>
    /// The food a barcode is linked to (through the <see cref="Barcode"/> table) with the barcode's own row (its pack); both null when it is linked to none. The food's Product serving is returned too (null without one).
    /// </summary>
    public Task<(Food? Food, Barcode? Barcode, ProductServing? Serving)> FindWithBarcodeAsync(string barcode) =>
        gate.RunAsync(async () =>
        {
            Food? found = null;
            Barcode? row = null;
            ProductServing? serving = null;
            await connection.RunInTransactionAsync(db =>
            {
                row = db.FindWithQuery<Barcode>("SELECT * FROM Barcode WHERE Barcode = ?", barcode);
                found = row is null ? null : db.Find<Food>(row.FoodId);
                if (found is not null)
                {
                    serving = ProductServingOf(db, found.Id);
                }
            });

            return (found, found is null ? null : row, serving);
        });

    /// <summary>The barcodes among <paramref name="barcodes"/> that are linked to a stored food.</summary>
    public Task<HashSet<string>> StoredBarcodesAsync(IReadOnlyCollection<string> barcodes) =>
        gate.RunAsync(async () =>
        {
            var found = new HashSet<string>();
            await connection.RunInTransactionAsync(db =>
            {
                // Chunked so a long list stays under SQLite's limit on bound variables.
                foreach (var chunk in barcodes.Distinct().Chunk(500))
                {
                    var marks = string.Join(",", chunk.Select(_ => "?"));
                    found.UnionWith(db.Query<Barcode>($"SELECT * FROM Barcode WHERE Barcode IN ({marks})", chunk.Cast<object>().ToArray()).Select(b => b.Code));
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
                    servings = ServingsOf(db, id);
                }
            });

            return (found, servings);
        });

    /// <summary>The servings of a food, product first, then packs, then the person's own, each by amount. Runs on the caller's connection, inside the gate.</summary>
    internal static List<Serving> ServingsOf(SQLiteConnection db, int foodId) =>
        db.Query<Serving>("SELECT * FROM Serving WHERE FoodId = ? ORDER BY SortOrder, Amount, Id", foodId);

    /// <summary>The food's Product serving row as a <see cref="ProductServing"/>, or null when it has none. Runs on the caller's connection, inside the gate.</summary>
    private static ProductServing? ProductServingOf(SQLiteConnection db, int foodId)
    {
        var product = db.FindWithQuery<Serving>("SELECT * FROM Serving WHERE FoodId = ? AND Source = ?", foodId, (int)ServingSource.Product);
        return product is null ? null : new ProductServing(product.Label, product.Amount);
    }

    /// <summary>
    /// Writes the serving rows a looked-up food and its product serving give (see <see cref="Foods.ServingRows"/>): the Product row is added or, when the food has one already, changed in place so its id stays; the Pack row is added unless its amount is stored already.
    /// A food with neither serving part and <see cref="Food.ServingChecked"/> true (the source flags its serving as wrong) loses its Product row, and the entries that used it keep their amounts with no serving link. Rows the person added are never touched.
    /// </summary>
    private static void WriteServings(SQLiteConnection db, int foodId, Food fresh, ProductServing? serving)
    {
        var rows = Foods.ServingRows.Of(fresh, serving);
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

    /// <summary>Links the barcode to the food with the food's pack, unless the barcode is linked already (it stays with its food). Runs on the caller's connection, inside the gate.</summary>
    private static void AddBarcode(SQLiteConnection db, int foodId, string barcode, Food pack, DateTimeOffset seen) =>
        db.Execute(
            "INSERT OR IGNORE INTO Barcode (FoodId, Barcode, PackageAmount, PackageLabel, FirstSeen) VALUES (?, ?, ?, ?, ?)",
            foodId, barcode, pack.PackageAmount, pack.PackageLabel, seen.UtcTicks);

    /// <summary>
    /// Stores a looked-up product under its barcode in one transaction, so two imports of one barcode, or of one product, never make two foods:
    /// a barcode that is stored already gives its food and changes nothing (<see cref="ImportOutcome.Known"/>);
    /// a product with the stored food's source id is that food, refreshed, its servings written and linked to the barcode (also <see cref="ImportOutcome.Known"/>);
    /// a stored Open Food Facts food that <see cref="Foods.ProductMatch.IsSameProduct"/> accepts gets the barcode, its pack and, when it has no Product serving, the product's (<see cref="ImportOutcome.Attached"/>; the food's name, brand and nutrients are not changed);
    /// otherwise a new food is inserted with its servings and barcode (<see cref="ImportOutcome.Created"/>).
    /// </summary>
    public Task<ImportResult> ImportAsync(string barcode, Food product, DateTimeOffset now, ProductServing? serving = null) =>
        gate.RunAsync(async () =>
        {
            ImportResult result = default;
            await connection.RunInTransactionAsync(db =>
            {
                var row = db.FindWithQuery<Barcode>("SELECT * FROM Barcode WHERE Barcode = ?", barcode);
                var known = row is null ? null : db.Find<Food>(row.FoodId);
                if (known is not null)
                {
                    result = new ImportResult(ImportOutcome.Known, known, row!, ProductServingOf(db, known.Id));
                    return;
                }

                if (row is not null)
                {
                    // A link to a food that no longer exists would block the barcode for good.
                    db.Execute("DELETE FROM Barcode WHERE Id = ?", row.Id);
                }

                var sameSource = db.Table<Food>().FirstOrDefault(f => f.Source == product.Source && f.SourceId == product.SourceId);
                if (sameSource is not null)
                {
                    product.Id = sameSource.Id;
                    if (serving is null)
                    {
                        product.ServingChecked ??= sameSource.ServingChecked;
                    }

                    db.Update(product);
                    WriteServings(db, product.Id, product, serving);
                    AddBarcode(db, product.Id, barcode, product, now);
                    result = new ImportResult(ImportOutcome.Known, product, BarcodeRow(db, barcode), ProductServingOf(db, product.Id));
                    return;
                }

                var target = db.Query<Food>("SELECT * FROM Food WHERE Source = ? AND Unit = ?", (int)FoodSource.OpenFoodFacts, (int)product.Unit)
                    .FirstOrDefault(f => Foods.ProductMatch.IsSameProduct(f, product));
                if (target is not null)
                {
                    AddBarcode(db, target.Id, barcode, product, now);
                    AttachServings(db, target.Id, product, serving);
                    result = new ImportResult(ImportOutcome.Attached, target, BarcodeRow(db, barcode), ProductServingOf(db, target.Id));
                    return;
                }

                db.Insert(product);
                WriteServings(db, product.Id, product, serving);
                AddBarcode(db, product.Id, barcode, product, now);
                result = new ImportResult(ImportOutcome.Created, product, BarcodeRow(db, barcode), ProductServingOf(db, product.Id));
            });

            switch (result.Outcome)
            {
                case ImportOutcome.Created:
                    Log.Inserted(logger, result.Food.Id);
                    break;
                case ImportOutcome.Attached:
                    Log.Updated(logger, result.Food.Id);
                    break;
            }

            return result;
        });

    private static Barcode BarcodeRow(SQLiteConnection db, string barcode) =>
        db.FindWithQuery<Barcode>("SELECT * FROM Barcode WHERE Barcode = ?", barcode);

    /// <summary>
    /// Adds the serving rows of a product attached to an existing food: its Pack row (not added when the food has one of that amount) and its Product row only when the food has none, so the food keeps the serving it had.
    /// </summary>
    private static void AttachServings(SQLiteConnection db, int foodId, Food product, ProductServing? serving)
    {
        foreach (var row in Foods.ServingRows.Of(product, serving))
        {
            if (row.Source == ServingSource.Product
                && db.ExecuteScalar<int>("SELECT COUNT(*) FROM Serving WHERE FoodId = ? AND Source = ?", foodId, (int)ServingSource.Product) > 0)
            {
                continue;
            }

            db.Execute(
                "INSERT OR IGNORE INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (?, ?, ?, ?, ?)",
                foodId, row.Label, row.Amount, (int)row.Source, row.SortOrder);
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
    /// Sets the nutrients (kcal, protein, carbs, fat, fibre, sugars, saturates, salt), the source details (brands, generic name, package, category, popularity, completeness, source update and fetch time) and the search text of the food with <paramref name="id"/> from <paramref name="fresh"/>; every other column stays as stored.
    /// The serving rows follow <see cref="WriteServings"/>: the product's serving and pack are written, a serving the source flags as wrong is removed and the food marked checked, and rows the person added stay. The food id and the diary entries are untouched.
    /// With <paramref name="barcode"/>, the pack of that barcode's row (amount and label) is set from <paramref name="fresh"/> too, and the Pack serving of its previous size is removed unless another barcode of the food has that size; the food's other barcodes keep theirs.
    /// False when no food has that id.
    /// </summary>
    public Task<bool> RefreshAsync(int id, Food fresh, string? barcode = null, ProductServing? serving = null) =>
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
                    WriteServings(db, id, fresh, serving);
                    if (barcode is not null)
                    {
                        DropReplacedPack(db, id, barcode, fresh.PackageAmount);
                        db.Execute("UPDATE Barcode SET PackageAmount = ?, PackageLabel = ? WHERE Barcode = ? AND FoodId = ?", fresh.PackageAmount, fresh.PackageLabel, barcode, id);
                    }
                }
            });

            if (changed)
            {
                Log.Updated(logger, id);
            }

            return changed;
        });

    /// <summary>
    /// When the barcode's pack size changed, removes the Pack serving of its previous size unless another barcode of the food still has that size; entries that used it keep their amounts with no serving link. Call before the barcode row is updated.
    /// </summary>
    private static void DropReplacedPack(SQLiteConnection db, int foodId, string barcode, double? newAmount)
    {
        var old = db.FindWithQuery<Barcode>("SELECT * FROM Barcode WHERE Barcode = ? AND FoodId = ?", barcode, foodId)?.PackageAmount;
        if (old is null || old == newAmount)
        {
            return;
        }

        var shared = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Barcode WHERE FoodId = ? AND Barcode <> ? AND PackageAmount = ?", foodId, barcode, old.Value);
        if (shared == 0)
        {
            DeleteServings(db, "FoodId = ? AND Source = ? AND Amount = ?", foodId, (int)ServingSource.Pack, old.Value);
        }
    }

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
                    db.Execute($"DELETE FROM Barcode WHERE FoodId IN ({marks})", args);
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

/// <summary>What <see cref="FoodStore.ImportAsync"/> did.</summary>
internal enum ImportOutcome
{
    /// <summary>A new food was stored.</summary>
    Created,

    /// <summary>The barcode was added to a stored food that is the same product in another size.</summary>
    Attached,

    /// <summary>The barcode, or the product's source id, was stored already.</summary>
    Known,
}

/// <summary>The food an import ended on and the row of the barcode that was imported.</summary>
/// <param name="Serving">The food's Product serving as stored after the import; null when it has none.</param>
internal readonly record struct ImportResult(ImportOutcome Outcome, Food Food, Barcode Barcode, ProductServing? Serving);

internal sealed class IdRow
{
    public int Id { get; set; }

    public string SourceId { get; set; } = "";
}

/// <summary>How many foods <see cref="FoodStore.UpsertManyAsync"/> inserted and updated.</summary>
internal readonly record struct UpsertManyResult(int Inserted, int Updated);
