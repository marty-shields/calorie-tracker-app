using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 5: adds the empty <c>ServingLabel</c> and <c>ServingAmount</c> columns to Food. A stored food with no serving is looked up again on its next scan.
/// Safe on a database with no Food table, or one that already has the columns.
/// </summary>
internal sealed class FoodServingUpgrade : ISchemaUpgrade
{
    public int Version => 5;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'") == 0)
        {
            return;
        }

        AddColumn(db, "ServingLabel", "varchar");
        AddColumn(db, "ServingAmount", "float");
    }

    private static void AddColumn(SQLiteConnection db, string name, string type)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = ?", name) == 0)
        {
            db.Execute($"ALTER TABLE Food ADD COLUMN {name} {type}");
        }
    }
}
