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

    [Given(@"^a barcode is held in front of the camera$")]
    public void GivenABarcodeIsHeldInFrontOfTheCamera() =>
        AppiumSetup.Adb.WriteText($"{AppiumSetup.App.ExternalFilesFolder}/{ScanSeamFileName}", ScanSeamBarcode);

    [Then(@"^Scan shows the found product with Add to diary$")]
    public void ThenScanShowsTheFoundProduct()
    {
        web.WaitUntil(scan.ShowsProduct, "Scan didn't show the found product");
        Assert.That(scan.HasAddToDiary(), Is.True, "Add to diary");
    }

    [When(@"^I tap Add to diary on Scan$")]
    public void WhenITapAddToDiaryOnScan() => scan.AddToDiary();

    /// <summary>The file <c>E2EScanSeam</c> reads; <c>ScanSeamValuesTests</c> checks both constants match it.</summary>
    private const string ScanSeamFileName = "e2e-barcode.txt";

    /// <summary>The barcode <c>E2EScanSeam</c> in the Debug app finds the first "oat" Ingredient for.</summary>
    private const string ScanSeamBarcode = "5012345678900";
}
