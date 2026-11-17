using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 16: drops what nothing reads any more: the <c>FoodBarcode</c> table (step 14 copied it into <c>Barcode</c>) and the <c>Food.ServingLabel</c> and <c>Food.ServingAmount</c> columns
/// (a food's own serving is its Product row in <c>Serving</c>, which step 13 filled from these columns and every write since keeps). Diary entries, servings and barcodes are not touched.
/// Safe on a database with none of them, and when run again.
/// </summary>
internal sealed class StorageCleanupUpgrade : ISchemaUpgrade
{
    public int Version => 16;

    public void Apply(SQLiteConnection db)
    {
        db.Execute("DROP TABLE IF EXISTS \"FoodBarcode\"");
        foreach (var column in new[] { "ServingLabel", "ServingAmount" })
        {
            if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = ?", column) > 0)
            {
                db.Execute($"ALTER TABLE Food DROP COLUMN {column}");
            }
        }
    }
}
