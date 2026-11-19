namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>Scan: the chip, and the camera or the message when it can't open. Not found needs a barcode: bUnit covers it, and the Diary journeys feature reaches a found product (which opens Serving) through the Debug-build scan seam.</summary>
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

    /// <summary>The chip under the top bar, such as "Adding to Dinner · 18 Nov".</summary>
    public string Chip()
    {
        web.WaitUntil(() => web.Exists("scan-adding"), "Scan didn't show what it adds to");
        return web.Text("scan-adding");
    }

    /// <summary>Whether a bottom bar shows. Scan has none.</summary>
    public bool HasBottomBar() => web.Exists("bottom-bar") || web.Exists("bottom-nav");

    public void SearchByName() => web.Tap("scan-search");
}
