using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes <see cref="DiaryEntry"/> rows, each call through the <see cref="DatabaseGate"/>. Removing only marks a row, so it can be restored.</summary>
internal sealed class DiaryStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>Adds the entry, ignoring its id and removed flag, and returns it with its id. Null, adding nothing, when no food has the entry's food id.</summary>
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
                };
                db.Insert(added);
            });

            return added;
        });

    /// <summary>Sets the day, meal, amount and servings of a kept entry, leaving its food and place. False when no kept entry has that id.</summary>
    public Task<bool> UpdateAsync(int id, string date, Meal meal, double amount, double? servings) =>
        gate.RunAsync(async () =>
            await connection.ExecuteAsync(
                "UPDATE DiaryEntry SET Date = ?, Meal = ?, Amount = ?, Servings = ? WHERE Id = ? AND IsRemoved = 0",
                date, (int)meal, amount, servings, id) > 0);

    /// <summary>Marks the entry removed, or kept again with false. False when no entry with that id is in the other state.</summary>
    public Task<bool> SetRemovedAsync(int id, bool removed) =>
        gate.RunAsync(async () =>
            await connection.ExecuteAsync(
                "UPDATE DiaryEntry SET IsRemoved = ? WHERE Id = ? AND IsRemoved = ?",
                removed, id, !removed) > 0);

    /// <summary>The kept entries of the day in the order added.</summary>
    public Task<List<DiaryEntry>> ListAsync(string date) =>
        gate.RunAsync(() => connection.QueryAsync<DiaryEntry>(
            "SELECT * FROM DiaryEntry WHERE Date = ? AND IsRemoved = 0 ORDER BY Id", date));
}
