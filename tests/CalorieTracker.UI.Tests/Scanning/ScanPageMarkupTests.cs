namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The App can't be referenced, so its scan page markup is read.</summary>
public class ScanPageMarkupTests
{
    [TestCase("Ean13")]
    [TestCase("Ean8")]
    [TestCase("Upca")]
    public void ScanPage_ListsTheSymbology(string symbology) =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml"), Does.Match(@"BarcodeSymbologies=""[^""]*\b" + symbology + @"\b"));
}
