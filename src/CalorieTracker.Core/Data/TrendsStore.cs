using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads what the trends need in one <see cref="DatabaseGate"/> call and one transaction, so the entries and their foods agree with each other.</summary>
internal sealed class TrendsStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>The kept entries from <paramref name="from"/> to <paramref name="to"/> inclusive (<see cref="DiaryEntry.Date"/> text) in the order added, and the foods they point at (a food that no longer exists is missing from <see cref="TrendsRead.Foods"/>).</summary>
    public Task<TrendsRead> ReadAsync(string from, string to) =>
        gate.RunAsync(async () =>
        {
            TrendsRead? read = null;
            await connection.RunInTransactionAsync(db =>
            {
                var entries = DiaryStore.ListRange(db, from, to);
                var foods = FoodStore.FindIn(db, entries.Select(e => e.FoodId).ToList()).ToDictionary(f => f.Id);
                read = new TrendsRead(entries, foods);
            });

            return read!;
        });
}

/// <summary>What <see cref="TrendsStore.ReadAsync"/> read.</summary>
internal sealed record TrendsRead(IReadOnlyList<DiaryEntry> Entries, IReadOnlyDictionary<int, Food> Foods);
