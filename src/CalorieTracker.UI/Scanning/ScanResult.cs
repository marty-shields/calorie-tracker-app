namespace CalorieTracker.UI.Scanning;

/// <summary>How a scan ended. <see cref="Digits"/> is set only for <see cref="ScanStatus.Scanned"/>.</summary>
public sealed record ScanResult(ScanStatus Status, string? Digits = null)
{
    public static ScanResult Scanned(string digits) => new(ScanStatus.Scanned, digits);

    /// <summary>The first non-empty value, kept verbatim; null when there is none.</summary>
    public static ScanResult? FromRawValues(IEnumerable<string?> rawValues)
    {
        var digits = rawValues.FirstOrDefault(v => !string.IsNullOrEmpty(v));
        return digits is null ? null : Scanned(digits);
    }

    public static ScanResult Cancelled { get; } = new(ScanStatus.Cancelled);

    public static ScanResult PermissionDenied { get; } = new(ScanStatus.PermissionDenied);

    public static ScanResult Unavailable { get; } = new(ScanStatus.Unavailable);
}

public enum ScanStatus
{
    Scanned,
    Cancelled,
    PermissionDenied,

    /// <summary>The scanner could not open: no window, or an error opening it.</summary>
    Unavailable,
}
