using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Links a barcode (EAN-8 or EAN-13 digits) to the food it identifies. A food can have several barcodes; a barcode has one food.</summary>
public class FoodBarcode
{
    [PrimaryKey]
    public string Barcode { get; set; } = "";

    [Indexed, NotNull]
    public int FoodId { get; set; }
}
