using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.Docs;

/// <summary>
/// The README's screenshots and Project layout against the docs and folders that must list them. Screenshot names come from the README's
/// <c>docs/images/readme/&lt;name&gt;-light.png</c> references and the first column of the table in docs/dev-setup-build.md.
/// Layout names are the <c>CalorieTracker.*</c> entries in the README's fenced Project layout block.
/// </summary>
public partial class ReadmeDocsTests
{
    private static readonly string Readme = RepoFiles.Read("README.md");

    [Test]
    public void Screenshots_AreAllInTheSetupTable()
    {
        var table = ScreenshotRow().Matches(RepoFiles.Read("docs", "dev-setup-build.md")).Select(m => m.Groups[1].Value);

        Assert.That(table, Is.EquivalentTo(ReadmeScreenshot().Matches(Readme).Select(m => m.Groups[1].Value).Distinct()));
    }

    [Test]
    public void YourData_SaysABarcodeLeavesThePhone()
    {
        Assert.That(Readme, Does.Contain("sends that barcode to an external product database (Open Food Facts)"));
    }

    [Test]
    public void Notice_CreditsBothDatasets()
    {
        var notice = RepoFiles.Read("NOTICE");

        Assert.That(notice, Does.Contain("Composition of Foods Integrated Dataset").And.Contain("Open Government Licence v3.0")
            .And.Contain("Open Food Facts").And.Contain("Open Database Licence"));
    }

    [Test]
    public void SourceProjects_AreAllInTheProjectLayout()
    {
        var layout = LayoutEntry().Matches(Readme).Select(m => m.Groups[1].Value);
        var projects = Directory.GetDirectories(RepoFiles.PathOf("src"), "CalorieTracker.*").Select(Path.GetFileName);

        Assert.That(layout, Is.SupersetOf(projects));
    }

    [GeneratedRegex(@"docs/images/readme/([a-z-]+)-light\.png")]
    private static partial Regex ReadmeScreenshot();

    [GeneratedRegex(@"^\| `([a-z-]+)` \| `[^`]+` \|\r?$", RegexOptions.Multiline)]
    private static partial Regex ScreenshotRow();

    [GeneratedRegex(@"^  (CalorieTracker\.[A-Za-z0-9.]+)/ ", RegexOptions.Multiline)]
    private static partial Regex LayoutEntry();
}
