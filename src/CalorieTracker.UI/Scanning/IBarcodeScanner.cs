namespace CalorieTracker.UI.Scanning;

public interface IBarcodeScanner
{
    /// <summary>Opens the camera and returns the first EAN-13, EAN-8 or UPC-A code seen, or why it ended without one.</summary>
    Task<ScanResult> ScanAsync();
}
