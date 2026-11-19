using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>The App can't be referenced, so its pages are read to check every bottom navigation route opens one.</summary>
public class NavRouteTests
{
    [Test]
    public void EveryNavDestinationRoute_HasAPageInTheApp()
    {
        var pages = RepoFiles.Find("*.razor", "src", "CalorieTracker.App", "Components", "Pages")
            .Select(f => File.ReadAllText(f))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(NavDestinations.All.Select(i => i.Route), Is.EqualTo(new[] { "", "trends" }));
            foreach (var item in NavDestinations.All)
            {
                Assert.That(pages.Any(p => p.Contains($"@page \"/{item.Route}\"")), Is.True, item.Route);
            }
        });
    }
}
