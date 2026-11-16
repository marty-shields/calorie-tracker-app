using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads and writes which first-use notices were shown, in <see cref="AppSettings"/>, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed class NoticeStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>Whether the Open Food Facts search notice was shown (false when nothing is saved).</summary>
    public Task<bool> OnlineSearchSeenAsync() =>
        gate.RunAsync(async () => (await connection.FindAsync<AppSettings>(AppSettings.RowId))?.OnlineSearchNoticeSeen == true);

    /// <summary>Marks the Open Food Facts search notice as shown. Changes only its own column.</summary>
    public Task SetOnlineSearchSeenAsync() =>
        gate.RunAsync(() => connection.RunInTransactionAsync(db =>
        {
            db.Execute("INSERT OR IGNORE INTO AppSettings (Id) VALUES (?)", AppSettings.RowId);
            db.Execute("UPDATE AppSettings SET OnlineSearchNoticeSeen = 1 WHERE Id = ?", AppSettings.RowId);
        }));
}
