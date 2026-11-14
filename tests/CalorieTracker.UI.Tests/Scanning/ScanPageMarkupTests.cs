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
    public void ScanPage_OffersEachDetectionToTheCallerInsteadOfClosingOnTheFirst() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml.cs"), Does.Contain("ScanResult.FromRawValues(e.BarcodeResults.Select(b => b.RawValue), accept)"));

    [Test]
    public void Scanner_PassesTheCallerAcceptToTheModal() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "CameraBarcodeScanner.cs"), Does.Contain("new ScanPage(accept)"));

    [Test]
    public void Manifest_DeclaresTheCameraPermission() =>
        Assert.That(Manifest(), Does.Match(@"<uses-permission\s+android:name=""android\.permission\.CAMERA"""));

    [Test]
    public void Manifest_DeclaresTheCameraAsOptional() =>
        Assert.That(Manifest(), Does.Match(@"<uses-feature\s+android:name=""android\.hardware\.camera""\s+android:required=""false"""));

    private static string Manifest() => RepoFiles.Read("src", "CalorieTracker.App", "Platforms", "Android", "AndroidManifest.xml");
}
