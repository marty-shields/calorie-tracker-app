using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads what the trends need in one <see cref="DatabaseGate"/> call and one transaction, so the entries, their foods and the streak agree with each other.</summary>
internal sealed class TrendsStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>
    /// The kept entries from <paramref name="from"/> to <paramref name="to"/> inclusive (<see cref="DiaryEntry.Date"/> text) in the order added, the foods they point at (a food that no longer exists is missing from <see cref="TrendsRead.Foods"/>),
    /// and the streak of days with an entry counted back from <paramref name="today"/> (see <see cref="TrendsRead.Streak"/>), whatever the range.
    /// </summary>
    public Task<TrendsRead> ReadAsync(string from, string to, DateOnly today) =>
        gate.RunAsync(async () =>
        {
            TrendsRead? read = null;
            await connection.RunInTransactionAsync(db =>
            {
                var entries = DiaryStore.ListRange(db, from, to);
                var foods = FoodStore.FindIn(db, entries.Select(e => e.FoodId).ToList()).ToDictionary(f => f.Id);
                var (streak, anyBefore) = ReadStreak(db, today);
                read = new TrendsRead(entries, foods, streak, anyBefore);
            });

            return read!;
        });

    // Reads the distinct kept days newest first and stops at the first gap, so a long streak costs one read of its own length and no more.
    // A day before today is seen by then unless today is the only day, so the same read says whether any past day was ever logged.
    private static (int Streak, bool AnyBefore) ReadStreak(SQLiteConnection db, DateOnly today)
    {
        var command = db.CreateCommand("SELECT DISTINCT Date FROM DiaryEntry WHERE IsRemoved = 0 AND Date <= ? ORDER BY Date DESC", DiaryEntry.FormatDate(today));
        var streak = 0;
        var anyBefore = false;
        DateOnly? expected = null;
        foreach (var text in command.ExecuteQueryScalars<string>())
        {
            var day = DiaryEntry.ParseDate(text);
            anyBefore |= day < today;
            expected ??= day == today ? today : today.AddDays(-1);
            if (day != expected)
            {
                break;
            }

            streak++;
            expected = day.AddDays(-1);
        }

        return (streak, anyBefore);
    }
}

/// <summary>What <see cref="TrendsStore.ReadAsync"/> read.</summary>
/// <param name="Streak">Days in a row with a kept entry, back from today, or from yesterday when today has none; a later day never counts.</param>
/// <param name="AnyBefore">Whether any kept entry is on a day before today, whatever the range.</param>
internal sealed record TrendsRead(IReadOnlyList<DiaryEntry> Entries, IReadOnlyDictionary<int, Food> Foods, int Streak, bool AnyBefore);
