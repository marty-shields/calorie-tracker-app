namespace CalorieTracker.UI.Models.Scan;

/// <summary>What the scan screen is showing.</summary>
public enum ScanState
{
    /// <summary>The camera is open, waiting for a barcode.</summary>
    Scanning,

    /// <summary>A barcode was detected and is being looked up.</summary>
    LookingUp,

    /// <summary>The product was found.</summary>
    Found,

    /// <summary>Any other lookup outcome. The screen never says which.</summary>
    NotFound,

    /// <summary>The camera could not open: permission was refused or the scanner failed.</summary>
    CameraUnavailable,
}
