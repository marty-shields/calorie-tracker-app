using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 9: adds the empty <c>OriginalName</c> and <c>FoodGroup</c> columns to Food and forgets the stored CoFID seed version, so the seed reloads once and fills them. Food ids are kept.
/// Safe on a database with no Food or SeedVersion table, or one that already has the columns.
/// </summary>
internal sealed class FoodOriginalNameUpgrade : ISchemaUpgrade
{
    public int Version => 9;

    public void Apply(SQLiteConnection db)
    {
        if (TableExists(db, "Food"))
        {
            foreach (var column in new[] { "OriginalName", "FoodGroup" })
            {
                if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = ?", column) == 0)
                {
                    db.Execute($"ALTER TABLE Food ADD COLUMN {column} varchar");
                }
            }
        }

        if (TableExists(db, "SeedVersion"))
        {
            db.Execute("DELETE FROM SeedVersion WHERE Source = ?", (int)FoodSource.CoFID);
        }
    }

    private static bool TableExists(SQLiteConnection db, string name) =>
        db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", name) > 0;
}
