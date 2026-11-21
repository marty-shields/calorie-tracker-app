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
}
