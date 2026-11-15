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

    /// <summary>Sets the target, or removes it with null. Changes only the target column, so other settings added later are kept.</summary>
    public Task SetAsync(int? kcal) =>
        gate.RunAsync(() => connection.RunInTransactionAsync(db =>
        {
            db.Execute("INSERT OR IGNORE INTO AppSettings (Id) VALUES (?)", AppSettings.RowId);
            db.Execute("UPDATE AppSettings SET DailyTargetKcal = ? WHERE Id = ?", kcal, AppSettings.RowId);
        }));
}
