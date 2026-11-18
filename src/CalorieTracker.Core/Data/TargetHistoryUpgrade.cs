using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Step 20: creates the <c>TargetHistory</c> table. A target set at that moment becomes one row from <c>0001-01-01</c>, so every past day uses it. Foods, diary and favourites are untouched. Safe to run again.</summary>
internal sealed class TargetHistoryUpgrade : ISchemaUpgrade
{
    public int Version => 20;

    public void Apply(SQLiteConnection db)
    {
        db.Execute("CREATE TABLE IF NOT EXISTS \"TargetHistory\" (\"EffectiveFrom\" varchar primary key not null, \"Kcal\" float)");
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AppSettings'") == 0)
        {
            return;
        }

        db.Execute("INSERT OR IGNORE INTO TargetHistory (EffectiveFrom, Kcal) SELECT '0001-01-01', DailyTargetKcal FROM AppSettings WHERE Id = ? AND DailyTargetKcal IS NOT NULL", AppSettings.RowId);
    }
}
