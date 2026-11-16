using System.Globalization;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 15: gives upgraded foods the Pack serving a refreshed or newly looked-up food gets. Adds a Pack row for each pack of a food that is above zero and at most 1000 g or ml
/// and not the amount of one of its other servings: first the pack of each <c>Barcode</c> row (so a food that holds several barcodes gets several packs), then the pack on the <c>Food</c> row.
/// Step 13 only did this for a food with no serving of its own. A pack over 1000 stays on its <c>Barcode</c> row only. A diary entry keeps its amount, servings count and serving, so its calories do not change.
/// Safe on a database with no Food, Barcode or Serving table, and when run again (a row is never added for an amount the food already has).
/// </summary>
internal sealed class PackServingUpgrade : ISchemaUpgrade
{
    // Frozen here, not read from ServingRows or FoodService, so a later change to the limit or the wording does not change what this step did.
    private const double MaxPackAmount = 1000;
    private const int PackSource = 1;
    private const int UserSource = 2;
    private const int MillilitreUnit = 1;
    private static readonly int[] CanMillilitres = [330, 355, 440, 473];

    public int Version => 15;

    public void Apply(SQLiteConnection db)
    {
        if (!TableExists(db, "Food") || !TableExists(db, "Serving"))
        {
            return;
        }

        var packs = new List<PackRow>();
        if (TableExists(db, "Barcode"))
        {
            packs.AddRange(db.Query<PackRow>(
                "SELECT f.Id AS FoodId, f.Unit AS Unit, b.PackageAmount AS Amount, b.PackageLabel AS Label FROM Barcode b JOIN Food f ON f.Id = b.FoodId WHERE b.PackageAmount IS NOT NULL ORDER BY b.Id"));
        }

        if (HasColumn(db, "Food", "PackageAmount") && HasColumn(db, "Food", "PackageLabel"))
        {
            packs.AddRange(db.Query<PackRow>("SELECT Id AS FoodId, Unit, PackageAmount AS Amount, PackageLabel AS Label FROM Food WHERE PackageAmount IS NOT NULL ORDER BY Id"));
        }

        foreach (var pack in packs)
        {
            if (!double.IsFinite(pack.Amount) || pack.Amount <= 0 || pack.Amount > MaxPackAmount)
            {
                continue;
            }

            var same = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Serving WHERE FoodId = ? AND Source <> ? AND Amount = ?", pack.FoodId, UserSource, pack.Amount);
            if (same > 0)
            {
                continue;
            }

            db.Execute(
                "INSERT OR IGNORE INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (?, ?, ?, ?, ?)",
                pack.FoodId, PackLabel(pack.Amount, pack.Unit, pack.Label), pack.Amount, PackSource, PackSource);
        }
    }

    private static string PackLabel(double amount, int unit, string? packageLabel)
    {
        var millilitres = unit == MillilitreUnit;
        var size = amount >= 1000
            ? $"{(amount / 1000).ToString("0.##", CultureInfo.InvariantCulture)} {(millilitres ? "L" : "kg")}"
            : $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} {(millilitres ? "ml" : "g")}";
        return $"{PackKind(amount, millilitres, packageLabel)} {size}";
    }

    private static string PackKind(double amount, bool millilitres, string? packageLabel)
    {
        var words = packageLabel is null ? [] : packageLabel.ToLowerInvariant().Split([' ', ',', '.', '(', ')', '-', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w is "can" or "cans" or "tin" or "canette"))
        {
            return "Can";
        }

        if (words.Any(w => w is "bottle" or "bottles" or "bouteille" or "btl"))
        {
            return "Bottle";
        }

        if (!millilitres)
        {
            return "Pack";
        }

        return amount >= 1000 ? "Bottle" : CanMillilitres.Contains((int)amount) && amount == Math.Floor(amount) ? "Can" : "Pack";
    }

    private static bool TableExists(SQLiteConnection db, string table) =>
        db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", table) > 0;

    private static bool HasColumn(SQLiteConnection db, string table, string column) =>
        db.ExecuteScalar<int>($"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = ?", column) > 0;

    private sealed class PackRow
    {
        public int FoodId { get; set; }

        public int Unit { get; set; }

        public double Amount { get; set; }

        public string? Label { get; set; }
    }
}
