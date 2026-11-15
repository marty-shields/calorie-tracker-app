using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads what a day summary needs in one <see cref="DatabaseGate"/> call and one transaction, so the target, the entries and the foods agree with each other.</summary>
internal sealed class DaySummaryStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>The saved target (null when none), the kept entries of the day in the order added, and the foods they point at; a food that no longer exists is missing from <see cref="DayRead.Foods"/>.</summary>
    public Task<DayRead> ReadDayAsync(string date) =>
        gate.RunAsync(async () =>
        {
            DayRead? read = null;
            // The transaction is the read-consistency backstop: the gate already keeps other writers out, but a write that bypassed it must not split the three reads.
            await connection.RunInTransactionAsync(db =>
            {
                var target = TargetStore.Get(db);
                var entries = DiaryStore.ListDay(db, date);
                var foods = FoodStore.FindIn(db, entries.Select(e => e.FoodId).ToList()).ToDictionary(f => f.Id);

                read = new DayRead(target, entries, foods);
            });

            return read!;
        });
}

/// <summary>What <see cref="DaySummaryStore.ReadDayAsync"/> read.</summary>
internal sealed record DayRead(int? TargetKcal, IReadOnlyList<DiaryEntry> Entries, IReadOnlyDictionary<int, Food> Foods);
