namespace CalorieTracker.UI.Scanning;

/// <summary>How a scan ended. <see cref="Digits"/> is set only for <see cref="ScanStatus.Scanned"/>.</summary>
public sealed record ScanResult(ScanStatus Status, string? Digits = null)
{
    public static ScanResult Scanned(string digits) => new(ScanStatus.Scanned, digits);

    public static ScanResult Cancelled { get; } = new(ScanStatus.Cancelled);

    public static ScanResult PermissionDenied { get; } = new(ScanStatus.PermissionDenied);
}

public enum ScanStatus
{
    Scanned,
    Cancelled,
    PermissionDenied,
}
