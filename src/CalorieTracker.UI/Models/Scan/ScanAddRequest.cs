namespace CalorieTracker.UI.Models.Scan;

/// <summary>What the scan screen hands on when Add to diary is tapped.</summary>
/// <param name="FoodId">Id of the stored food.</param>
public sealed record ScanAddRequest(int FoodId);
