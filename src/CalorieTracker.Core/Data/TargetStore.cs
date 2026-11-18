using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes the daily target in <see cref="AppSettings"/>, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed class TargetStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>The saved target, or null when none is set.</summary>
    public Task<int?> GetAsync() =>
        gate.RunAsync<int?>(async () => (await connection.FindAsync<AppSettings>(AppSettings.RowId))?.DailyTargetKcal);

    /// <summary>The saved target, or null when none is set, on the caller's connection, inside the gate.</summary>
    internal static int? Get(SQLiteConnection db) => db.Find<AppSettings>(AppSettings.RowId)?.DailyTargetKcal;

    /// <summary>
    /// Sets the target, or removes it with null, and writes <paramref name="today"/>'s history row (replacing that day's row) in the same transaction.
    /// Changes only the target column, so other settings added later are kept.
    /// </summary>
    public Task SetAsync(int? kcal, DateOnly today) =>
        gate.RunAsync(() => connection.RunInTransactionAsync(db =>
        {
            db.Execute("INSERT OR IGNORE INTO AppSettings (Id) VALUES (?)", AppSettings.RowId);
            db.Execute("UPDATE AppSettings SET DailyTargetKcal = ? WHERE Id = ?", kcal, AppSettings.RowId);
            db.Execute("INSERT OR REPLACE INTO TargetHistory (EffectiveFrom, Kcal) VALUES (?, ?)", Key(today), kcal);
        }));

    /// <summary>The history rows dated up to <paramref name="to"/>, oldest first, in one read.</summary>
    public Task<List<TargetHistory>> UpToAsync(DateOnly to) =>
        gate.RunAsync(() => connection.QueryAsync<TargetHistory>("SELECT EffectiveFrom, Kcal FROM TargetHistory WHERE EffectiveFrom <= ? ORDER BY EffectiveFrom", Key(to)));

    internal static string Key(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
