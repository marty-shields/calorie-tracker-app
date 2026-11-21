namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>The App's and the stories' host pages load the kit's theme script, tokens and base styles, and no tokens of this app's own.</summary>
public class HostPageTests
{
    private static readonly string[] Order =
    [
        "_content/Shields.UI/theme.js",
        "_content/Shields.UI/css/tokens.css",
        "_content/Shields.UI/css/base.css",
    ];

    [TestCase("CalorieTracker.App")]
    [TestCase("CalorieTracker.UI.Stories.Host")]
    public void HostPage_LoadsTheKitsThemeTokensAndBaseStylesInOrder(string project)
    {
        var page = RepoFiles.Read("src", project, "wwwroot", project == "CalorieTracker.App" ? "index.html" : "iframe.html");

        var positions = Order.Select(path => page.IndexOf(path, StringComparison.Ordinal)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(positions, Does.Not.Contain(-1), "a kit file is not loaded");
            Assert.That(positions, Is.Ordered);
            Assert.That(page, Does.Contain("""<script src="_content/Shields.UI/theme.js" data-storage-key="calorietracker.theme"></script>"""), "the key the release before the kit stored the theme under");
            Assert.That(page, Does.Not.Contain("CalorieTracker.UI/theme.js").And.Not.Contain("CalorieTracker.UI/css/"), "this app has no tokens or base styles of its own");
        });
    }

    [TestCase("iframe.html")]
    [TestCase("index.html")]
    public void StoriesHost_LoadsTheStoriesStylesAndTheirTokensAfterTheKitTokens(string file)
    {
        var page = RepoFiles.Read("src", "CalorieTracker.UI.Stories.Host", "wwwroot", file);

        var blazorUi = page.IndexOf("_content/Shields.UI.Stories/css/blazor-ui.css", StringComparison.Ordinal);
        var storiesTokens = page.IndexOf("_content/Shields.UI.Stories/css/tokens.css", StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(blazorUi, Is.GreaterThanOrEqualTo(0), "blazor-ui.css is not loaded");
            Assert.That(storiesTokens, Is.GreaterThanOrEqualTo(0), "the stories tokens.css is not loaded");
            if (file == "iframe.html")
            {
                Assert.That(storiesTokens, Is.GreaterThan(page.IndexOf("_content/Shields.UI/css/tokens.css", StringComparison.Ordinal)), "the stories tokens come after the kit's");
            }
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
