using CalorieTracker.UI.Components.Charts;

namespace CalorieTracker.UI.Tests.Components.Charts;

public class EnergyShareBarTests : ComponentTestBase
{
    [Test]
    public void Render_ShowsGramsAndSharesInTheTextAlternative()
    {
        var cut = Context.Render<EnergyShareBar>(p => p
            .Add(x => x.Protein, 30).Add(x => x.Carbs, 67).Add(x => x.Fat, 29).Add(x => x.TestId, "share"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=share]").GetAttribute("aria-label"),
                Is.EqualTo("Energy share: protein 30 g, 18% of calories; carbs 67 g, 41%; fat 29 g, 40%."));
            Assert.That(cut.Find("[data-testid=share-protein]").TextContent, Is.EqualTo("Protein 30 g"));
            Assert.That(cut.Find("[data-testid=share-carbs]").TextContent, Is.EqualTo("Carbs 67 g"));
            Assert.That(cut.Find("[data-testid=share-fat]").TextContent, Is.EqualTo("Fat 29 g"));
        });
    }

    [Test]
    public void Render_SizesSegmentsByCalories()
    {
        var cut = Context.Render<EnergyShareBar>(p => p
            .Add(x => x.Protein, 10).Add(x => x.Carbs, 10).Add(x => x.Fat, 10).Add(x => x.TestId, "share"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=share-protein-segment]").GetAttribute("style"), Does.Contain("flex-grow: 40"));
            Assert.That(cut.Find("[data-testid=share-carbs-segment]").GetAttribute("style"), Does.Contain("flex-grow: 40"));
            Assert.That(cut.Find("[data-testid=share-fat-segment]").GetAttribute("style"), Does.Contain("flex-grow: 90"));
        });
    }

    [Test]
    public void Render_AllZero_ShowsNoSegmentsAndSaysSo()
    {
        var cut = Context.Render<EnergyShareBar>(p => p.Add(x => x.TestId, "share"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".segment"), Is.Empty);
            Assert.That(cut.Find("[data-testid=share]").GetAttribute("aria-label"),
                Is.EqualTo("Energy share: protein 0 g, carbs 0 g, fat 0 g. Nothing eaten yet."));
        });
    }
}
