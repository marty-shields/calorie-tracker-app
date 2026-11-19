using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes <see cref="DiaryEntry"/> rows, each call through the <see cref="DatabaseGate"/>. Removing only marks a row, so it can be restored.</summary>
internal sealed class DiaryStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>Adds the entry, ignoring its id and removed flag, and returns it with its id. Null, adding nothing, when no food has the entry's food id. A <see cref="DiaryEntry.ServingId"/> that is not a serving of that food is saved as null.</summary>
    public Task<DiaryEntry?> AddAsync(DiaryEntry entry) =>
        gate.RunAsync<DiaryEntry?>(async () =>
        {
            DiaryEntry? added = null;
            await connection.RunInTransactionAsync(db =>
            {
                if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM Food WHERE Id = ?", entry.FoodId) == 0)
                {
                    return;
                }

                added = new DiaryEntry
                {
                    Date = entry.Date,
                    Meal = entry.Meal,
                    FoodId = entry.FoodId,
                    Amount = entry.Amount,
                    Servings = entry.Servings,
                    ServingId = entry.ServingId is { } serving && db.ExecuteScalar<int>("SELECT COUNT(*) FROM Serving WHERE Id = ? AND FoodId = ?", serving, entry.FoodId) > 0 ? serving : null,
                };
                db.Insert(added);
            });

            return added;
        });

    /// <summary>
    /// Sets the day, meal, amount, servings and serving of a kept entry, leaving its food. A moved entry keeps its original position in the order added (the list orders by id). False when no kept entry has that id.
    /// A <paramref name="servingId"/> that is not a serving of the entry's food is saved as null.
    /// </summary>
    public Task<bool> UpdateAsync(int id, string date, Meal meal, double amount, double? servings, int? servingId = null) =>
        gate.RunAsync(async () =>
            await connection.ExecuteAsync(
                "UPDATE DiaryEntry SET Date = ?, Meal = ?, Amount = ?, Servings = ?, ServingId = (SELECT Id FROM Serving WHERE Id = ? AND FoodId = DiaryEntry.FoodId) WHERE Id = ? AND IsRemoved = 0",
                date, (int)meal, amount, servings, servingId, id) > 0);

    /// <summary>Marks the entry removed, or kept again with false. False when no entry with that id is in the other state.</summary>
    public Task<bool> SetRemovedAsync(int id, bool removed) =>
        gate.RunAsync(async () =>
            await connection.ExecuteAsync(
                "UPDATE DiaryEntry SET IsRemoved = ? WHERE Id = ? AND IsRemoved = ?",
                removed, id, !removed) > 0);

    /// <summary>The kept entries of the day in the order added.</summary>
    public Task<List<DiaryEntry>> ListAsync(string date) =>
        gate.RunAsync(() => connection.QueryAsync<DiaryEntry>(ListDaySql, date));

    private const string ListDaySql = "SELECT * FROM DiaryEntry WHERE Date = ? AND IsRemoved = 0 ORDER BY Id";

    /// <summary>The kept entries of the day in the order added, on the caller's connection, inside the gate.</summary>
    internal static List<DiaryEntry> ListDay(SQLiteConnection db, string date) => db.Query<DiaryEntry>(ListDaySql, date);

    /// <summary>The kept entries from <paramref name="from"/> to <paramref name="to"/> inclusive (both <see cref="DiaryEntry.Date"/> text) in the order added, on the caller's connection, inside the gate.</summary>
    internal static List<DiaryEntry> ListRange(SQLiteConnection db, string from, string to) =>
        db.Query<DiaryEntry>("SELECT * FROM DiaryEntry WHERE Date BETWEEN ? AND ? AND IsRemoved = 0 ORDER BY Id", from, to);
}
