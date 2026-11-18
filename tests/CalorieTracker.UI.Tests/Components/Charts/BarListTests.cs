using CalorieTracker.UI.Components.Charts;

namespace CalorieTracker.UI.Tests.Components.Charts;

public class BarListTests : ComponentTestBase
{
    private IRenderedComponent<BarList> Render(IReadOnlyList<BarListItem> items, double? max = null) =>
        Context.Render<BarList>(p => p.Add(x => x.Items, items).Add(x => x.Max, max).Add(x => x.Label, "Calories by meal").Add(x => x.TestId, "list"));

    [Test]
    public void Render_ShowsEachRowsLabelAndValueText()
    {
        var cut = Render([new("Breakfast", 420, "420 kcal"), new("Lunch", 1234)]);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=list]").GetAttribute("aria-label"), Is.EqualTo("Calories by meal"));
            Assert.That(cut.Find("[data-testid=list-label-0]").TextContent, Is.EqualTo("Breakfast"));
            Assert.That(cut.Find("[data-testid=list-value-0]").TextContent, Is.EqualTo("420 kcal"));
            Assert.That(cut.Find("[data-testid=list-value-1]").TextContent, Is.EqualTo("1,234"));
        });
    }

    [Test]
    public void Render_SizesBarsAgainstTheLargestValue()
    {
        var cut = Render([new("A", 200), new("B", 800), new("C", 0)]);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=list-bar-0]").GetAttribute("style"), Does.Contain("--fill: 0.25"));
            Assert.That(cut.Find("[data-testid=list-bar-1]").GetAttribute("style"), Does.Contain("--fill: 1"));
            Assert.That(cut.Find("[data-testid=list-bar-2]").GetAttribute("style"), Does.Contain("--fill: 0"));
        });
    }

    [Test]
    public void Render_WithMax_SizesBarsAgainstIt()
    {
        var cut = Render([new("Nov", 15)], max: 30);

        Assert.That(cut.Find("[data-testid=list-bar-0]").GetAttribute("style"), Does.Contain("--fill: 0.5"));
    }

    [Test]
    public void Render_AllZero_FillsNothing()
    {
        var cut = Render([new("A", 0), new("B", 0)]);

        Assert.That(cut.FindAll(".fill").Select(f => f.GetAttribute("style")), Has.All.Contain("--fill: 0"));
    }

    [Test]
    public void Render_NoRows_SaysSo()
    {
        var cut = Context.Render<BarList>(p => p.Add(x => x.EmptyText, "No foods logged yet").Add(x => x.TestId, "list"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=list-empty]").TextContent, Is.EqualTo("No foods logged yet"));
            Assert.That(cut.FindAll("li"), Is.Empty);
        });
    }
}
