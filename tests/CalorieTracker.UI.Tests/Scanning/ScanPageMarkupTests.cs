namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The App can't be referenced, so its scan page markup is read.</summary>
public class ScanPageMarkupTests
{
    [TestCase("Ean13")]
    [TestCase("Ean8")]
    [TestCase("Upca")]
    public void ScanPage_ListsTheSymbology(string symbology) =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml"), Does.Match(@"BarcodeSymbologies=""[^""]*\b" + symbology + @"\b"));

    [Test]
    public void ScanPage_ShowsTheHintOverTheCamera() =>
        Assert.That(ScanXaml(), Does.Match(@"<Label[^>]*AutomationId=""scan-native-hint""[^>]*Text=""Point the camera at a barcode"""));

    [Test]
    public void ScanPage_KeepsACancelButtonInTheBottomRowWiredToCancel() =>
        Assert.That(ScanXaml(), Does.Match(@"<Grid Grid\.Row=""1""[\s\S]*<Button AutomationId=""scan-native-cancel""[^>]*Text=""Cancel""[^>]*Clicked=""OnCancelClicked"""));

    [Test]
    public void ScanPage_HidesTheDecorativeFrameFromScreenReaders() =>
        Assert.That(ScanXaml(), Does.Match(@"<Border AutomationId=""scan-native-frame""\s+AutomationProperties\.IsInAccessibleTree=""False"""));

    [Test]
    public void ScanPage_UsesNoLiteralColoursInTheMarkup() =>
        Assert.That(ScanXaml(), Does.Not.Match(@"Color=""#"));

    [Test]
    public void ScanPage_OffersEachDetectionToTheCallerInsteadOfClosingOnTheFirst() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml.cs"), Does.Contain("ScanResult.FromRawValues(e.BarcodeResults.Select(b => b.RawValue), accept)"));

    [Test]
    public void ScanPage_TakesOnlyTheFirstOutcome() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml.cs"), Does.Match(@"if \(!completion\.TrySetResult\(result\)\)\s*\{\s*return;"));

    [Test]
    public void Scanner_PassesTheCallerAcceptToTheModal() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "CameraBarcodeScanner.cs"), Does.Contain("new ScanPage(accept)"));

    [Test]
    public void Manifest_DeclaresTheCameraPermission() =>
        Assert.That(Manifest(), Does.Match(@"<uses-permission\s+android:name=""android\.permission\.CAMERA"""));

    [Test]
    public void Manifest_DeclaresTheCameraAsOptional() =>
        Assert.That(Manifest(), Does.Match(@"<uses-feature\s+android:name=""android\.hardware\.camera""\s+android:required=""false"""));

    private static string ScanXaml() => RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml");

    private static string Manifest() => RepoFiles.Read("src", "CalorieTracker.App", "Platforms", "Android", "AndroidManifest.xml");
}
