using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 18: deletes every serving that came from a product (the Product and Pack rows) and drops <c>Food.ServingChecked</c>, which only said whether a product had been asked for one.
/// A diary entry added by one of those servings keeps its amount, servings count and calories; only its <c>ServingId</c> is cleared. The person's own servings are not touched.
/// Each statement runs only when its table or column exists, so it is safe on a database with none of them, and when run again.
/// </summary>
internal sealed class DropImportedServingsUpgrade : ISchemaUpgrade
{
    // Frozen copies of ServingSource.Product and Pack, so a later change to the enum cannot change what this step deletes.
    private const int Product = 0;
    private const int Pack = 1;

    public int Version => 18;

    public void Apply(SQLiteConnection db)
    {
        if (TableExists(db, "Serving"))
        {
            if (TableExists(db, "DiaryEntry"))
            {
                db.Execute($"UPDATE DiaryEntry SET ServingId = NULL WHERE ServingId IN (SELECT Id FROM Serving WHERE Source IN ({Product}, {Pack}))");
            }

            db.Execute($"DELETE FROM Serving WHERE Source IN ({Product}, {Pack})");
        }

        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'ServingChecked'") > 0)
        {
            db.Execute("ALTER TABLE Food DROP COLUMN ServingChecked");
        }
    }

    private static bool TableExists(SQLiteConnection db, string name) =>
        db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", name) > 0;
}
