namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>Scan: the camera, or the message when it can't open. Found and Not found need a real barcode, so bUnit covers them.</summary>
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
}
