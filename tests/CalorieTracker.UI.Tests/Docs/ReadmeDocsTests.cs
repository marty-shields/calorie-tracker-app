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
        var section = YourData();

        Assert.That(section, Does.Contain("barcode").And.Contain("Open Food Facts").And.Contain("none of your food log or other data").And.Contain("Google ML Kit").And.Contain("usage metrics")
            .And.Not.Contain("Nothing is sent to a server"));
    }

    [Test]
    public void YourData_SaysTheDiaryAndTargetStayOnThePhoneAndWhatIsStored()
    {
        var section = YourData();

        Assert.That(section, Does.Contain("Your diary, daily target and foods are stored on the phone and in its Android backup")
            .And.Contain("A diary entry holds the food id, the amount (g or ml, and the servings and which serving if you entered them), the meal, the day and whether it was removed.")
            .And.Contain("The daily target is one number of calories.")
            .And.Contain("The theme is kept in the app's web storage, not the database.")
            .And.Contain("The app sends none of your diary, daily target or settings anywhere."));
        Assert.That(RepoFiles.Read("SECURITY.md"), Does.Contain("README.md#your-data").And.Contain("stay on the phone and in its Android backup"));
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

    private static string YourData()
    {
        var start = Readme.IndexOf("## Your data", StringComparison.Ordinal);
        var end = Readme.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        return Readme[start..end];
    }

    [GeneratedRegex(@"docs/images/readme/([a-z-]+)-light\.png")]
    private static partial Regex ReadmeScreenshot();

    [GeneratedRegex(@"^\| `([a-z-]+)` \| `[^`]+` \|\r?$", RegexOptions.Multiline)]
    private static partial Regex ScreenshotRow();

    [GeneratedRegex(@"^  (CalorieTracker\.[A-Za-z0-9.]+)/ ", RegexOptions.Multiline)]
    private static partial Regex LayoutEntry();
}
