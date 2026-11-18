using CalorieTracker.UI.Components.Charts;

namespace CalorieTracker.UI.Tests.Components.Charts;

public class StatTileTests : ComponentTestBase
{
    [Test]
    public void Render_ShowsFigureAndCaption()
    {
        var cut = Context.Render<StatTile>(p => p.Add(x => x.Figure, "1,980").Add(x => x.Caption, "avg kcal a day").Add(x => x.TestId, "tile"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=tile-figure]").TextContent, Is.EqualTo("1,980"));
            Assert.That(cut.Find("[data-testid=tile-caption]").TextContent, Is.EqualTo("avg kcal a day"));
            Assert.That(cut.FindAll("[data-testid=tile-note]"), Is.Empty);
        });
    }

    [Test]
    public void Render_NotEnoughDays_ReplacesTheFigureAndKeepsTheCaption()
    {
        var cut = Context.Render<StatTile>(p => p
            .Add(x => x.Figure, "1,980").Add(x => x.Caption, "avg kcal a day").Add(x => x.NotEnoughDays, true).Add(x => x.TestId, "tile"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=tile-figure]").TextContent, Is.EqualTo("–"));
            Assert.That(cut.Find("[data-testid=tile-caption]").TextContent, Is.EqualTo("avg kcal a day"));
            Assert.That(cut.Find("[data-testid=tile-note]").TextContent, Is.EqualTo("Not enough days yet"));
            Assert.That(cut.Markup, Does.Not.Contain("1,980"));
        });
    }
}
