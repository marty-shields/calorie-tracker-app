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

    /// <summary>The characters that split <see cref="Food.SearchText"/> and typed queries into words.</summary>
    internal static readonly char[] Separators = [' ', '\t', '\r', '\n', ',', '(', ')', '/', '-'];

    private static readonly string Haystack = BuildHaystack();

    /// <summary>
    /// Finds foods where every term starts a word of <see cref="Food.SearchText"/>, split on <see cref="Separators"/>. Terms must be lowercase.
    /// Foods where every term is a whole word come first, then by name. Scans the table, which is fine at the seed's size.
    /// </summary>
    public Task<List<Food>> SearchAsync(IReadOnlyList<string> terms, int limit, CancellationToken ct = default) =>
        gate.RunAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var haystack = Haystack;
            var escaped = terms.Select(t => t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")).ToList();
            var prefix = string.Join(" AND ", escaped.Select(_ => $"{haystack} LIKE ? ESCAPE '\\'"));
            var args = new List<object>();
            args.AddRange(escaped.Select(t => "% " + t + "%"));
            args.AddRange(escaped.Select(t => "% " + t + " %"));
            args.Add(limit);
            return await connection.QueryAsync<Food>(
                $"SELECT * FROM Food WHERE {prefix} ORDER BY CASE WHEN {prefix} THEN 0 ELSE 1 END, Name COLLATE NOCASE, Id LIMIT ?",
                args.ToArray());
        });

    private static string BuildHaystack()
    {
        var text = "SearchText";
        foreach (var c in Separators.Where(c => c != ' '))
        {
            text = $"replace({text}, char({(int)c}), ' ')";
        }

        return $"(' ' || {text} || ' ')";
    }

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
