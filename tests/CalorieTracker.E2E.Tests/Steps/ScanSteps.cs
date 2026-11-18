using CalorieTracker.Core.Diary;
using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class ScanSteps(ScanScreen scan, Native native, DeviceClock clock, WebView web)
{
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

    [Then(@"^Scan says it is adding to (Breakfast|Lunch|Dinner|Snacks) today$")]
    public void ThenScanSaysItIsAddingTo(string meal) =>
        web.Eventually(scan.Chip, Is.EqualTo($"Adding to {meal} · {Today()}"), "the Scan chip");

    [Then(@"^Scan says it is adding to a meal today$")]
    public void ThenScanSaysItIsAddingToAMealToday()
    {
        // The clock is read either side of the render: across a window boundary either meal is right.
        var before = MealWindows.MealFor(TimeOnly.FromDateTime(clock.LocalNow()));
        web.Eventually(scan.Chip, Does.Match(@$"^Adding to (Breakfast|Lunch|Dinner|Snacks) · {Today()}$"), "the Scan chip");
        var after = MealWindows.MealFor(TimeOnly.FromDateTime(clock.LocalNow()));
        Assert.That(scan.Chip(), Is.AnyOf($"Adding to {before} · {Today()}", $"Adding to {after} · {Today()}"), "the meal of the time of day");
    }

    [Then(@"^Scan has no bottom bar$")]
    public void ThenScanHasNoBottomBar() => Assert.That(scan.HasBottomBar(), Is.False);

    [When(@"^I tap Search by name instead on Scan$")]
    public void WhenITapSearchByNameInsteadOnScan() => scan.SearchByName();

    private string Today() => clock.LocalNow().ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The file <c>E2EScanSeam</c> reads; <c>ScanSeamValuesTests</c> checks both constants match it.</summary>
    private const string ScanSeamFileName = "e2e-barcode.txt";

    /// <summary>The barcode <c>E2EScanSeam</c> in the Debug app finds the top search result for "oat" for.</summary>
    private const string ScanSeamBarcode = "5012345678900";
}
