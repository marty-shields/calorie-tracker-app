using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// One barcode of a food, with the pack it sits on. A food has one or more (a 330 ml can, a 1 L bottle); a barcode belongs to one food.
/// Replaces the <c>FoodBarcode</c> table, which schema step 14 copied from and step 16 dropped.
/// </summary>
public class Barcode
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed]
    public int FoodId { get; set; }

    /// <summary>EAN-8 or EAN-13 digits (see <see cref="Foods.BarcodeNormaliser"/>). Unique.</summary>
    [NotNull, Column("Barcode"), Indexed("UX_Barcode_Barcode", 1, Unique = true)]
    public string Code { get; set; } = "";

    /// <summary>The pack size in grams, or millilitres to match the food's unit. Null when the source gives none, or in the food's other unit.</summary>
    public double? PackageAmount { get; set; }

    /// <summary>The pack size as the source words it, such as "330 ml". Null when the source gives none.</summary>
    public string? PackageLabel { get; set; }

    /// <summary>When the barcode was first stored (UTC). For a barcode that schema step 14 copied, the food's fetch time, or the time of the step.</summary>
    [NotNull]
    public DateTimeOffset FirstSeen { get; set; }
}
