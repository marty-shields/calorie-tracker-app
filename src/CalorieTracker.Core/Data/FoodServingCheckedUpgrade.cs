using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 8: adds the empty <c>ServingChecked</c> column to Food, so a stored food is looked up for a serving again until it is marked checked.
/// Safe on a database with no Food table, or one that already has the column.
/// </summary>
internal sealed class FoodServingCheckedUpgrade : ISchemaUpgrade
{
    public int Version => 8;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'") == 0)
        {
            return;
        }

        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'ServingChecked'") == 0)
        {
            db.Execute("ALTER TABLE Food ADD COLUMN ServingChecked INTEGER");
        }
    }
}
