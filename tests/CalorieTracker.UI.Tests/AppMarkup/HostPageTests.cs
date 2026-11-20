namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>The App's and the stories' host pages load the kit's theme script, tokens and base styles, then this app's own tokens.</summary>
public class HostPageTests
{
    private static readonly string[] Order =
    [
        "_content/Shields.UI/theme.js",
        "_content/Shields.UI/css/tokens.css",
        "_content/Shields.UI/css/base.css",
        "_content/CalorieTracker.UI/css/tokens.css",
    ];

    [TestCase("CalorieTracker.App")]
    [TestCase("CalorieTracker.UI.Stories.Host")]
    public void HostPage_LoadsTheKitBeforeTheAppTokens(string project)
    {
        var page = RepoFiles.Read("src", project, "wwwroot", project == "CalorieTracker.App" ? "index.html" : "iframe.html");

        var positions = Order.Select(path => page.IndexOf(path, StringComparison.Ordinal)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(positions, Does.Not.Contain(-1), "a kit or app file is not loaded");
            Assert.That(positions, Is.Ordered);
            Assert.That(page, Does.Contain("""<script src="_content/Shields.UI/theme.js" data-storage-key="calorietracker.theme"></script>"""), "the key the release before the kit stored the theme under");
            Assert.That(page, Does.Not.Contain("CalorieTracker.UI/theme.js").And.Not.Contain("CalorieTracker.UI/css/base.css"));
        });
    }

    [Test]
    public void LocalThemeScriptAndBaseStyles_AreGone() =>
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(RepoFiles.PathOf("src", "CalorieTracker.UI", "wwwroot", "theme.js")), Is.False);
            Assert.That(File.Exists(RepoFiles.PathOf("src", "CalorieTracker.UI", "wwwroot", "css", "base.css")), Is.False);
        });
}
