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

    /// <summary>The foods with these ids; an id with no food is left out.</summary>
    public Task<List<Food>> FindAsync(IReadOnlyCollection<int> ids) =>
        gate.RunAsync(() => connection.QueryAsync<Food>(
            // Ids are ints, so joining them cannot inject SQL; it also avoids SQLite's limit on bound variables.
            $"SELECT * FROM Food WHERE Id IN ({string.Join(',', ids)})"));

    /// <summary>The characters that split <see cref="Food.SearchText"/> and typed queries into words.</summary>
    internal static readonly char[] Separators = [' ', '\t', '\r', '\n', ',', '(', ')', '/', '-'];

    private static readonly string Haystack = BuildHaystack();

    /// <summary>
    /// Finds foods where every term starts a word of <see cref="Food.SearchText"/>, split on <see cref="Separators"/>. Terms must be lowercase.
    /// Foods whose text starts with <paramref name="phrase"/> come first, then the other matches, each by name. Scans the table, which is fine at the seed's size.
    /// </summary>
    /// <param name="phrase">The whole folded query, every word in the order typed, joined by single spaces.</param>
    /// <param name="type">Only foods of this type; null for every type.</param>
    public Task<List<Food>> SearchAsync(IReadOnlyList<string> terms, string phrase, int limit, FoodType? type = null, CancellationToken ct = default) =>
        gate.RunAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var haystack = Haystack;
            var escaped = terms.Select(EscapeLike).ToList();
            var match = string.Join(" AND ", escaped.Select(_ => $"{haystack} LIKE ? ESCAPE '\\'"));
            var args = new List<object>();
            args.AddRange(escaped.Select(t => "% " + t + "%"));
            var typeClause = "";
            if (type is { } wanted)
            {
                typeClause = " AND Type = ?";
                args.Add((int)wanted);
            }

            var matches = await connection.QueryAsync<Food>(
                $"SELECT * FROM Food WHERE {match}{typeClause} ORDER BY Name COLLATE NOCASE, Id",
                args.ToArray());
            // Done here rather than in SQL because SQL cannot collapse a separator run of any length.
            var spaced = " " + phrase;
            return matches
                .Select(f => (Food: f, First: StartsWithPhrase(f.SearchText, spaced)))
                .OrderBy(m => m.First ? 0 : 1)
                .Select(m => m.Food)
                .Take(limit)
                .ToList();
        });

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static bool StartsWithPhrase(string searchText, string spacedPhrase) =>
        (" " + string.Join(' ', searchText.Split(Separators, StringSplitOptions.RemoveEmptyEntries)))
            .StartsWith(spacedPhrase, StringComparison.OrdinalIgnoreCase);

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

    /// <summary>Sets only the serving of the food with <paramref name="id"/>, leaving its nutrients as stored. False when no food has that id.</summary>
    public Task<bool> SetServingAsync(int id, string label, double amount) =>
        gate.RunAsync(async () =>
        {
            var changed = await connection.ExecuteAsync("UPDATE Food SET ServingLabel = ?, ServingAmount = ? WHERE Id = ?", label, amount, id) > 0;
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
