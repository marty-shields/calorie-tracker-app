using CalorieTracker.UI.Stories.Stories.Foundations;

namespace CalorieTracker.UI.Tests.Stories;

public class FoundationStoryWiringTests : BunitContext
{
    [Test]
    public void AppIconStory_GivesEachKitImageItsArt()
    {
        var cut = Render<AppIconStory>();

        var launcher = cut.FindAll(".mask img").Select(img => img.GetAttribute("src")).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(launcher.Where((_, i) => i % 2 == 0), Is.All.EqualTo(AppIconArt.Background));
            Assert.That(launcher.Where((_, i) => i % 2 == 1), Is.All.EqualTo(AppIconArt.Foreground));
            Assert.That(cut.FindAll(".silhouette").Select(e => e.GetAttribute("style")!), Is.Not.Empty.And.All.Contains(AppIconArt.Monochrome));
            Assert.That(cut.Find(".splash.light .splash-image").GetAttribute("src"), Is.EqualTo(AppIconArt.SplashLight));
            Assert.That(cut.Find(".splash.dark .splash-image").GetAttribute("src"), Is.EqualTo(AppIconArt.SplashDark));
        });
    }

    [Test]
    public void TokensStory_ShowsTheAppColoursAsThemedRowsAndTheSizesAsSizes()
    {
        JSInterop.SetupModule("./_content/Shields.UI.Stories/Shared/TokenSheet.razor.js")
            .Setup<Dictionary<string, string>>("read", _ => true).SetResult([]);

        var cut = Render<TokensStory>();

        Assert.Multiple(() =>
        {
            foreach (var colour in AppTokenNames.Colours)
            {
                Assert.That(cut.Find($"[data-token=\"{colour}\"]").QuerySelectorAll("td[data-theme]"), Has.Count.EqualTo(2), colour);
            }

            foreach (var size in AppTokenNames.Sizes)
            {
                Assert.That(cut.Find($"li[data-token=\"{size}\"]"), Is.Not.Null, size);
            }
        });
    }
}
