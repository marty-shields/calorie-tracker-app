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

    public int? LastServingId { get; set; }
}

/// <summary>Reads what the kept (not removed) <see cref="DiaryEntry"/> rows say about each food, each call through the <see cref="DatabaseGate"/>. Removed rows count for nothing.</summary>
internal sealed class FoodHistoryStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    // The last* columns are the food's newest kept entry on any day: in the given meal when it has one there (the join then takes one more bound value, the meal), else in any meal.
    private const string LastEntryAnyMeal = """
        JOIN DiaryEntry e ON e.Id = (SELECT MAX(Id) FROM DiaryEntry WHERE FoodId = g.FoodId AND IsRemoved = 0)
        """;

    private const string LastEntryInMeal = """
        JOIN DiaryEntry e ON e.Id = COALESCE(
            (SELECT MAX(Id) FROM DiaryEntry WHERE FoodId = g.FoodId AND IsRemoved = 0 AND Meal = ?),
            (SELECT MAX(Id) FROM DiaryEntry WHERE FoodId = g.FoodId AND IsRemoved = 0))
        """;

    private static string LastEntryJoin(Meal? meal) => meal is null ? LastEntryAnyMeal : LastEntryInMeal;

    /// <summary>The bound values in the order the query reads them: the ones before the join, the meal for the join when set, then the ones after.</summary>
    private static object[] Args(Meal? meal, object[] before, params object[] after) =>
        [.. before, .. meal is { } m ? new object[] { (int)m } : [], .. after];

    private const string Columns = "g.FoodId AS FoodId, g.Times AS TimesLogged, e.Id AS LastEntryId, e.Date AS LastDate, e.Amount AS LastAmount, e.Servings AS LastServings, e.ServingId AS LastServingId";

    /// <summary>Distinct foods, the food of the newest entry first. TimesLogged counts every kept entry of the food. The last values come from <paramref name="meal"/> when set (see <see cref="LastEntryInMeal"/>).</summary>
    public Task<List<FoodHistoryRow>> RecentAsync(int limit, Meal? meal = null) =>
        gate.RunAsync(() => connection.QueryAsync<FoodHistoryRow>(
            $"""
            SELECT {Columns}
            FROM (SELECT FoodId, COUNT(*) AS Times, MAX(Id) AS RecentId FROM DiaryEntry WHERE IsRemoved = 0 GROUP BY FoodId) g
            {LastEntryJoin(meal)}
            ORDER BY g.RecentId DESC
            LIMIT ?
            """,
            Args(meal, [], limit)));

    /// <summary>
    /// Distinct foods logged in the meal on or after <paramref name="since"/> (a <see cref="DiaryEntry.Date"/> text), most days first, then newest entry in that meal.
    /// TimesLogged is the number of days.
    /// </summary>
    public Task<List<FoodHistoryRow>> UsualAsync(Meal meal, string since, int limit) =>
        gate.RunAsync(() => connection.QueryAsync<FoodHistoryRow>(
            $"""
            SELECT {Columns}
            FROM (SELECT FoodId, COUNT(DISTINCT Date) AS Times, MAX(Id) AS RecentId FROM DiaryEntry WHERE IsRemoved = 0 AND Meal = ? AND Date >= ? GROUP BY FoodId) g
            {LastEntryJoin(meal)}
            ORDER BY g.Times DESC, g.RecentId DESC
            LIMIT ?
            """,
            Args(meal, [(int)meal, since], limit)));

    /// <summary>The foods among <paramref name="foodIds"/> that have a kept entry, in no set order; as <see cref="AllAsync"/> otherwise. Ids go in chunks, so a long list stays under SQLite's limit on bound values.</summary>
    public Task<List<FoodHistoryRow>> ByFoodIdsAsync(IReadOnlyCollection<int> foodIds, Meal? meal = null) =>
        gate.RunAsync(async () =>
        {
            var rows = new List<FoodHistoryRow>();
            foreach (var chunk in foodIds.Distinct().Chunk(IdChunk))
            {
                rows.AddRange(await connection.QueryAsync<FoodHistoryRow>(
                    $"""
                    SELECT {Columns}
                    FROM (SELECT FoodId, COUNT(*) AS Times FROM DiaryEntry WHERE IsRemoved = 0 AND FoodId IN ({string.Join(",", chunk.Select(_ => "?"))}) GROUP BY FoodId) g
                    {LastEntryJoin(meal)}
                    """,
                    Args(meal, [.. chunk.Cast<object>()])));
            }

            return rows;
        });

    private const int IdChunk = 500;

    /// <summary>Every food with a kept entry, in no set order; TimesLogged counts every kept entry. The last values come from <paramref name="meal"/> when set (see <see cref="LastEntryInMeal"/>).</summary>
    public Task<List<FoodHistoryRow>> AllAsync(Meal? meal = null) =>
        gate.RunAsync(() => connection.QueryAsync<FoodHistoryRow>(
            $"""
            SELECT {Columns}
            FROM (SELECT FoodId, COUNT(*) AS Times FROM DiaryEntry WHERE IsRemoved = 0 GROUP BY FoodId) g
            {LastEntryJoin(meal)}
            """,
            Args(meal, [])));
}
