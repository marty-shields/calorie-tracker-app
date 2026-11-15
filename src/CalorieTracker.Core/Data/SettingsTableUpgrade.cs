using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 6: creates the <c>AppSettings</c> table, with no row, so no daily target is set. Safe on a database that already has the table.
/// </summary>
internal sealed class SettingsTableUpgrade : ISchemaUpgrade
{
    public int Version => 6;

    public void Apply(SQLiteConnection db) =>
        db.Execute("CREATE TABLE IF NOT EXISTS \"AppSettings\" (\"Id\" integer not null primary key, \"DailyTargetKcal\" integer)");
}
