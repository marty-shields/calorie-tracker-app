using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class ScanSteps(ScanScreen scan, Journeys journeys, Native native, WebView web)
{
    [Given(@"^I have opened Scan$")]
    [When(@"^I open Scan from the navigation bar$")]
    public void OpenScan() => journeys.OpenScan();

    [Given(@"^I have refused the camera permission$")]
    [When(@"^I refuse the camera permission$")]
    public void RefuseTheCameraPermission() => native.DenyPermission();

    [Then(@"^Scan says the camera is unavailable$")]
    public void ThenScanSaysTheCameraIsUnavailable() =>
        web.Eventually(scan.CameraUnavailableText, Does.StartWith("Camera unavailable"), "camera-unavailable message");

    [Then(@"^Scan again is offered$")]
    public void ThenScanAgainIsOffered() => Assert.That(scan.HasScanAgain(), Is.True, "Scan again");
}
