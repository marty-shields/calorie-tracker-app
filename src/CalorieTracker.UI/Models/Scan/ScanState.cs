namespace CalorieTracker.UI.Models.Scan;

/// <summary>What the scan screen is showing.</summary>
public enum ScanState
{
    /// <summary>The moment before the native camera covers the screen.</summary>
    OpeningCamera,

    /// <summary>The camera was cancelled and is sliding away; the screen is about to leave.</summary>
    Closing,

    /// <summary>A barcode was detected and is being looked up.</summary>
    LookingUp,

    /// <summary>The product was found.</summary>
    Found,

    /// <summary>Any other lookup outcome. The screen never says which.</summary>
    NotFound,

    /// <summary>The camera could not open: permission was refused or the scanner failed.</summary>
    CameraUnavailable,
}
