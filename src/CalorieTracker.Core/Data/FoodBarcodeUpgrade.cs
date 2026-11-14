using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 2: moves <c>Food.Barcode</c> into the <c>FoodBarcode</c> table, then drops the column and its index.
/// Where several foods shared a barcode, the lowest id keeps it. Safe on a database with no Food table or no Barcode column.
/// </summary>
internal sealed class FoodBarcodeUpgrade : ISchemaUpgrade
{
    public int Version => 2;

    public void Apply(SQLiteConnection db)
    {
        // The table is created here because sqlite-net only adds tables after the last step.
        db.Execute("CREATE TABLE IF NOT EXISTS \"FoodBarcode\" (\"Barcode\" varchar primary key not null, \"FoodId\" integer not null)");
        db.Execute("CREATE INDEX IF NOT EXISTS \"FoodBarcode_FoodId\" ON \"FoodBarcode\"(\"FoodId\")");

        var hasColumn = db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Barcode'") > 0;
        if (!hasColumn)
        {
            return;
        }

        db.Execute("INSERT OR IGNORE INTO FoodBarcode (Barcode, FoodId) SELECT Barcode, MIN(Id) FROM Food WHERE Barcode IS NOT NULL GROUP BY Barcode");
        db.Execute("DROP INDEX IF EXISTS \"Food_Barcode\"");
        db.Execute("ALTER TABLE Food DROP COLUMN Barcode");
    }
}
