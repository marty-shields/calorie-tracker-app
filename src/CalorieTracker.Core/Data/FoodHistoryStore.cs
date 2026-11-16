using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>One food's kept diary entries in short: how often it was logged, and its most recent entry (the highest entry id).</summary>
internal sealed class FoodHistoryRow
{
    public int FoodId { get; set; }

    public int TimesLogged { get; set; }

    public int LastEntryId { get; set; }

    public string LastDate { get; set; } = "";

    public double LastAmount { get; set; }

    public double? LastServings { get; set; }
}

/// <summary>Reads what the kept (not removed) <see cref="DiaryEntry"/> rows say about each food, each call through the <see cref="DatabaseGate"/>. Removed rows count for nothing.</summary>
internal sealed class FoodHistoryStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    // The last* columns are always the food's newest kept entry, in any meal and on any day.
    private const string LastEntryJoin = """
        JOIN DiaryEntry e ON e.Id = (SELECT MAX(Id) FROM DiaryEntry WHERE FoodId = g.FoodId AND IsRemoved = 0)
        """;

    private const string Columns = "g.FoodId AS FoodId, g.Times AS TimesLogged, e.Id AS LastEntryId, e.Date AS LastDate, e.Amount AS LastAmount, e.Servings AS LastServings";

    /// <summary>Distinct foods, the food of the newest entry first. TimesLogged counts every kept entry of the food.</summary>
    public Task<List<FoodHistoryRow>> RecentAsync(int limit) =>
        gate.RunAsync(() => connection.QueryAsync<FoodHistoryRow>(
            $"""
            SELECT {Columns}
            FROM (SELECT FoodId, COUNT(*) AS Times, MAX(Id) AS RecentId FROM DiaryEntry WHERE IsRemoved = 0 GROUP BY FoodId) g
            {LastEntryJoin}
            ORDER BY g.RecentId DESC
            LIMIT ?
            """,
            limit));

    /// <summary>
    /// Distinct foods logged in the meal on or after <paramref name="since"/> (a <see cref="DiaryEntry.Date"/> text), most days first, then newest entry in that meal.
    /// TimesLogged is the number of days.
    /// </summary>
    public Task<List<FoodHistoryRow>> UsualAsync(Meal meal, string since, int limit) =>
        gate.RunAsync(() => connection.QueryAsync<FoodHistoryRow>(
            $"""
            SELECT {Columns}
            FROM (SELECT FoodId, COUNT(DISTINCT Date) AS Times, MAX(Id) AS RecentId FROM DiaryEntry WHERE IsRemoved = 0 AND Meal = ? AND Date >= ? GROUP BY FoodId) g
            {LastEntryJoin}
            ORDER BY g.Times DESC, g.RecentId DESC
            LIMIT ?
            """,
            (int)meal, since, limit));

    /// <summary>The foods among <paramref name="foodIds"/> that have a kept entry, in no set order; TimesLogged counts every kept entry.</summary>
    public Task<List<FoodHistoryRow>> ForFoodsAsync(IReadOnlyCollection<int> foodIds) =>
        gate.RunAsync(async () =>
        {
            var found = new List<FoodHistoryRow>();
            // Chunked so a long list stays under SQLite's limit on bound variables.
            foreach (var chunk in foodIds.Distinct().Chunk(500))
            {
                var marks = string.Join(",", chunk.Select(_ => "?"));
                found.AddRange(await connection.QueryAsync<FoodHistoryRow>(
                    $"""
                    SELECT {Columns}
                    FROM (SELECT FoodId, COUNT(*) AS Times FROM DiaryEntry WHERE IsRemoved = 0 AND FoodId IN ({marks}) GROUP BY FoodId) g
                    {LastEntryJoin}
                    """,
                    chunk.Cast<object>().ToArray()));
            }

            return found;
        });
}
