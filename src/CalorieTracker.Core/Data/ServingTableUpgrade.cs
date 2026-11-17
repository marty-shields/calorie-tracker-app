using System.Globalization;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 13: gives servings their own table. Creates <c>Serving</c>, moves each food's stored serving (<c>ServingLabel</c> and <c>ServingAmount</c>) into a Product row, adds a Pack row from <c>PackageAmount</c> for a food with no serving of its own whose pack is at most 1000 g or ml
/// (the rule of the release before this one, with its pack limit raised), and adds the empty <c>ServingId</c> column to DiaryEntry. A diary entry keeps its amount, servings count and so its calories.
/// <c>ServingChecked</c> keeps its meaning and stays on Food. The old Food columns stay in place and are not read again; step 16 (<see cref="StorageCleanupUpgrade"/>) drops <c>ServingLabel</c> and <c>ServingAmount</c>.
/// Safe on a database with no Food, DiaryEntry or Serving table, and when run again (rows are matched by food, source and amount).
/// </summary>
internal sealed class ServingTableUpgrade : ISchemaUpgrade
{
    // Frozen here, not read from FoodService, so a later change to the limit or the wording does not change what this step did.
    private const double MaxPackAmount = 1000;

    private const int ProductSource = 0;
    private const int PackSource = 1;

    public int Version => 13;

    public void Apply(SQLiteConnection db)
    {
        db.Execute("CREATE TABLE IF NOT EXISTS \"Serving\" (\"Id\" integer primary key autoincrement not null, \"FoodId\" integer not null, \"Label\" varchar not null, \"Amount\" float not null, \"Source\" integer not null, \"SortOrder\" integer not null)");
        db.Execute("CREATE UNIQUE INDEX IF NOT EXISTS \"UX_Serving_Food_Source_Amount\" ON \"Serving\" (\"FoodId\", \"Source\", \"Amount\")");

        if (TableExists(db, "DiaryEntry") && !HasColumn(db, "DiaryEntry", "ServingId"))
        {
            db.Execute("ALTER TABLE DiaryEntry ADD COLUMN ServingId INTEGER");
        }

        if (!TableExists(db, "Food"))
        {
            return;
        }

        if (HasColumn(db, "Food", "ServingLabel") && HasColumn(db, "Food", "ServingAmount"))
        {
            db.Execute(
                "INSERT OR IGNORE INTO Serving (FoodId, Label, Amount, Source, SortOrder) SELECT Id, TRIM(ServingLabel), ServingAmount, ?, ? FROM Food WHERE ServingLabel IS NOT NULL AND TRIM(ServingLabel) <> '' AND ServingAmount IS NOT NULL AND ServingAmount > 0",
                ProductSource, ProductSource);
        }

        if (HasColumn(db, "Food", "PackageAmount") && HasColumn(db, "Food", "ServingLabel") && HasColumn(db, "Food", "ServingAmount"))
        {
            var packs = db.Query<PackRow>(
                "SELECT Id, Unit, PackageAmount FROM Food WHERE PackageAmount IS NOT NULL AND PackageAmount > 0 AND PackageAmount <= ? AND NOT EXISTS (SELECT 1 FROM Serving WHERE Serving.FoodId = Food.Id AND Serving.Source = 0)", MaxPackAmount);
            foreach (var pack in packs)
            {
                db.Execute(
                    "INSERT OR IGNORE INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (?, ?, ?, ?, ?)",
                    pack.Id, PackLabel(pack.PackageAmount, pack.Unit), pack.PackageAmount, PackSource, PackSource);
            }
        }
    }

    private static string PackLabel(double amount, int unit) =>
        $"Whole pack ({amount.ToString("0.##", CultureInfo.InvariantCulture)} {(unit == (int)FoodUnit.Millilitre ? "ml" : "g")})";

    private static bool TableExists(SQLiteConnection db, string table) =>
        db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", table) > 0;

    private static bool HasColumn(SQLiteConnection db, string table, string column) =>
        db.ExecuteScalar<int>($"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = ?", column) > 0;

    private sealed class PackRow
    {
        public int Id { get; set; }

        public int Unit { get; set; }

        public double PackageAmount { get; set; }
    }
}
