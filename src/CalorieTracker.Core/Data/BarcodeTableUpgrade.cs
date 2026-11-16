using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 14: gives barcodes their own rows. Creates <c>Barcode</c> (id, food id, unique barcode, pack amount and label, first seen) and fills it, so every barcode already stored still resolves:
/// first from <c>FoodBarcode</c> (the lowest food id keeps a barcode two foods share), then from the <c>SourceId</c> of every Open Food Facts food that has no row for it yet, with that food's <c>PackageAmount</c> and <c>PackageLabel</c>.
/// A barcode that is in <c>FoodBarcode</c> and is also its food's source id gets the food's pack. First seen is the food's fetch time, or the time of the step when the food has none.
/// <c>FoodBarcode</c> stays in place, unread, for this release; a later step can drop it. Safe on a database with no Food or FoodBarcode table, and when run again.
/// </summary>
internal sealed class BarcodeTableUpgrade(TimeProvider time) : ISchemaUpgrade
{
    public int Version => 14;

    public void Apply(SQLiteConnection db)
    {
        db.Execute("CREATE TABLE IF NOT EXISTS \"Barcode\" (\"Id\" integer primary key autoincrement not null, \"FoodId\" integer not null, \"Barcode\" varchar not null, \"PackageAmount\" float, \"PackageLabel\" varchar, \"FirstSeen\" bigint not null)");
        db.Execute("CREATE INDEX IF NOT EXISTS \"Barcode_FoodId\" ON \"Barcode\" (\"FoodId\")");
        db.Execute("CREATE UNIQUE INDEX IF NOT EXISTS \"UX_Barcode_Barcode\" ON \"Barcode\" (\"Barcode\")");

        if (!TableExists(db, "Food"))
        {
            return;
        }

        var now = time.GetUtcNow().UtcTicks;
        var hasPack = HasColumn(db, "Food", "PackageAmount") && HasColumn(db, "Food", "PackageLabel");
        var hasFetched = HasColumn(db, "Food", "FetchedAt");
        var firstSeen = hasFetched ? "COALESCE(f.FetchedAt, ?)" : "?";
        var pack = hasPack ? "f.PackageAmount, f.PackageLabel" : "NULL, NULL";

        if (TableExists(db, "FoodBarcode"))
        {
            // The pack goes only with the food's own barcode: the other barcodes of a food were never given a pack of their own.
            db.Execute(
                $"INSERT OR IGNORE INTO Barcode (FoodId, Barcode, PackageAmount, PackageLabel, FirstSeen) SELECT f.Id, b.Barcode, CASE WHEN f.SourceId = b.Barcode THEN {(hasPack ? "f.PackageAmount" : "NULL")} END, CASE WHEN f.SourceId = b.Barcode THEN {(hasPack ? "f.PackageLabel" : "NULL")} END, {firstSeen} FROM FoodBarcode b JOIN Food f ON f.Id = b.FoodId ORDER BY b.FoodId, b.Barcode",
                now);
        }

        db.Execute(
            $"INSERT OR IGNORE INTO Barcode (FoodId, Barcode, PackageAmount, PackageLabel, FirstSeen) SELECT f.Id, f.SourceId, {pack}, {firstSeen} FROM Food f WHERE f.Source = ? ORDER BY f.Id",
            now, (int)FoodSource.OpenFoodFacts);
    }

    private static bool TableExists(SQLiteConnection db, string table) =>
        db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", table) > 0;

    private static bool HasColumn(SQLiteConnection db, string table, string column) =>
        db.ExecuteScalar<int>($"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = ?", column) > 0;
}
