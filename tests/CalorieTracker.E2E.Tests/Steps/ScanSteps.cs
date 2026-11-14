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
        web.Eventually(scan.CameraUnavailableText, Is.EqualTo("Couldn't open the camera"), "camera-unavailable message");

    [Then(@"^Try again is offered on Scan$")]
    public void ThenTryAgainIsOffered() => Assert.That(scan.HasTryAgain(), Is.True, "Try again");
}
