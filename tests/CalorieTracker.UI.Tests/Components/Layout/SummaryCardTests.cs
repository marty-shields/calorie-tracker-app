using CalorieTracker.UI.Components.Layout;

namespace CalorieTracker.UI.Tests.Components.Layout;

public class SummaryCardTests : ComponentTestBase
{
    [Test]
    public void Render_ShowsContent()
    {
        var cut = Context.Render<SummaryCard>(p => p
            .AddChildContent("<span>Eaten 659</span>")
            .Add(x => x.TestId, "card"));

        Assert.That(cut.Find("[data-testid=card] span").TextContent, Is.EqualTo("Eaten 659"));
    }

    [Test]
    public void Css_TakesSurfaceBorderAndRadiusFromTokens()
    {
        var css = File.ReadAllText(RepoFiles.Find("SummaryCard.razor.css", "src", "CalorieTracker.UI").Single());

        Assert.Multiple(() =>
        {
            Assert.That(css, Does.Match(@"background-color:\s*var\(--color-surface\)"));
            Assert.That(css, Does.Match(@"border:\s*var\(--border-width\) solid var\(--color-border\)"));
            Assert.That(css, Does.Match(@"border-radius:\s*var\(--radius-lg\)"));
        });
    }
}
