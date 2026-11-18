using CalorieTracker.UI.Components.Charts;

namespace CalorieTracker.UI.Tests.Components.Charts;

public class ChartLegendTests : ComponentTestBase
{
    [Test]
    public void Render_ShowsASwatchAndLabelForEachEntry()
    {
        var cut = Context.Render<ChartLegend>(p => p.Add(x => x.TestId, "legend").Add(x => x.Items,
        [
            new(LegendKind.OnTarget, "On target"), new(LegendKind.Over, "Over"), new(LegendKind.Under, "Under"),
            new(LegendKind.NotLogged, "Not logged"), new(LegendKind.TargetLine, "Target"), new(LegendKind.TargetBand, "±10%"),
        ]));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("li").Select(li => li.QuerySelector(".text")!.TextContent),
                Is.EqualTo(new[] { "On target", "Over", "Under", "Not logged", "Target", "±10%" }));
            Assert.That(cut.FindAll(".swatch").Select(s => s.ClassList.Last()),
                Is.EqualTo(new[] { "on", "over", "under", "not-logged", "line", "band" }));
            Assert.That(cut.FindAll("[data-testid^=legend-]"), Has.Count.EqualTo(6));
        });
    }

    [Test]
    public void Render_OverSwatchCarriesTheMarker()
    {
        var cut = Context.Render<ChartLegend>(p => p.Add(x => x.Items, [new(LegendKind.Over, "Over"), new(LegendKind.Under, "Under")]));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find(".swatch.over").TextContent, Is.EqualTo("▲"));
            Assert.That(cut.Find(".swatch.under").TextContent, Is.Empty);
        });
    }
}
