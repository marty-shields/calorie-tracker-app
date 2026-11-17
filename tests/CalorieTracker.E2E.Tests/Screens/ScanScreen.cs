namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>Scan: the camera, or the message when it can't open. Found and Not found need a barcode: bUnit covers them, and the Diary journeys feature reaches Found through the Debug-build scan seam.</summary>
public sealed class ScanScreen(WebView web)
{
    public const string Title = "Scan barcode";

    /// <summary>Whether the camera-unavailable error shows.</summary>
    public bool ShowsCameraUnavailable() => web.Exists("scan-camera-unavailable-error");

    /// <summary>The camera-unavailable error, once it shows.</summary>
    public string CameraUnavailableText()
    {
        web.WaitUntil(ShowsCameraUnavailable, "Scan didn't show the camera-unavailable message");
        return web.Text("scan-camera-unavailable-error");
    }

    /// <summary>Whether Try again shows on the camera-unavailable error.</summary>
    public bool HasTryAgain() => web.Exists("scan-camera-unavailable-retry");

    /// <summary>Whether a found product shows.</summary>
    public bool ShowsProduct() => web.Exists("scan-product");

    /// <summary>Whether Add to diary shows under a found product.</summary>
    public bool HasAddToDiary() => web.Exists("scan-add");

    /// <summary>The found product's heading.</summary>
    public string ProductTitle() => web.Text("scan-name");

    public void AddToDiary() => web.Tap("scan-add");
}
