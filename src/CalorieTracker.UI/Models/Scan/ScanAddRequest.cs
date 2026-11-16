namespace CalorieTracker.UI.Models.Scan;

/// <summary>What the scan screen hands on when Add to diary is tapped.</summary>
/// <param name="FoodId">Id of the stored food.</param>
/// <param name="PackAmount">The pack, in g or ml, of the barcode that was scanned. Null when unknown.</param>
public sealed record ScanAddRequest(int FoodId, double? PackAmount);
