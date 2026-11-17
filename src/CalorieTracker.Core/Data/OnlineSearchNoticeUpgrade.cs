using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 17: adds the empty <c>OnlineSearchNoticeSeen</c> column to AppSettings, so the first-use notice of the online search shows until it is marked seen.
/// Existing settings stay. Safe on a database with no AppSettings table, or one that already has the column.
/// </summary>
internal sealed class OnlineSearchNoticeUpgrade : ISchemaUpgrade
{
    public int Version => 17;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AppSettings'") == 0)
        {
            return;
        }

        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('AppSettings') WHERE name = 'OnlineSearchNoticeSeen'") == 0)
        {
            db.Execute("ALTER TABLE AppSettings ADD COLUMN OnlineSearchNoticeSeen INTEGER");
        }
    }
}
