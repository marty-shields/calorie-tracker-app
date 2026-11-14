using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The App can't be referenced, so its scanner source is read: its log lines may hold a status, never the barcode.</summary>
public partial class ScannerLoggingTests
{
    private static readonly string[] Files = ["MauiBarcodeScanner.cs", "ScanPage.xaml.cs"];

    [TestCaseSource(nameof(Files))]
    public void LogLines_CarryNoBarcodeContent(string file)
    {
        var source = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", file);

        var logged = Template().Matches(source).Select(m => m.Value).ToList();

        Assert.That(logged, Has.None.Matches(@"(?i)digits|raw|value|barcode"));
        Assert.That(source, Does.Not.Contain("logger.Log").And.Not.Contain("Debug.WriteLine"));
    }

    [Test]
    public void FinishedLogLine_TakesOnlyTheStatus()
    {
        var source = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "MauiBarcodeScanner.cs");

        Assert.That(source, Does.Contain("Log.Finished(logger, result.Status)"));
    }

    [GeneratedRegex("""\[LoggerMessage\([^\]]*\]""")]
    private static partial Regex Template();
}
