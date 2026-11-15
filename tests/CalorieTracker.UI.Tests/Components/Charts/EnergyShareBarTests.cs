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
                Is.EqualTo("Energy share: protein 30 g, 19% of calories; carbs 67 g, 41%; fat 29 g, 40%."));
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
            Assert.That(cut.Find("[data-testid=share-protein-segment]").GetAttribute("style"), Does.Contain("flex-grow: 23.529"));
            Assert.That(cut.Find("[data-testid=share-carbs-segment]").GetAttribute("style"), Does.Contain("flex-grow: 23.529"));
            Assert.That(cut.Find("[data-testid=share-fat-segment]").GetAttribute("style"), Does.Contain("flex-grow: 52.941"));
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

    [Test]
    public void Render_NegativeGrams_ShowsNoSegmentAndZeroGrams()
    {
        var cut = Context.Render<EnergyShareBar>(p => p
            .Add(x => x.Protein, -5).Add(x => x.Carbs, 10).Add(x => x.Fat, 10).Add(x => x.TestId, "share"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=share-protein-segment]"), Is.Empty);
            Assert.That(cut.Find("[data-testid=share-protein]").TextContent, Is.EqualTo("Protein 0 g"));
            Assert.That(cut.Find("[data-testid=share]").GetAttribute("aria-label"), Does.Contain("protein 0 g, 0%"));
        });
    }

    [Test]
    public void Render_FractionalGrams_KeepsTheDecimal()
    {
        var cut = Context.Render<EnergyShareBar>(p => p
            .Add(x => x.Protein, 12.5).Add(x => x.Carbs, 10).Add(x => x.Fat, 10).Add(x => x.TestId, "share"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=share-protein]").TextContent, Is.EqualTo("Protein 12.5 g"));
            Assert.That(cut.Find("[data-testid=share]").GetAttribute("aria-label"), Does.Contain("protein 12.5 g"));
        });
    }

    [TestCase(30, 30, 13, "34% of calories", "33%; fat 13 g, 33%")]
    [TestCase(30, 67, 29, "19% of calories", "41%; fat 29 g, 40%")]
    public void Render_PercentagesAddUpToOneHundred(double protein, double carbs, double fat, string first, string rest)
    {
        var cut = Context.Render<EnergyShareBar>(p => p
            .Add(x => x.Protein, protein).Add(x => x.Carbs, carbs).Add(x => x.Fat, fat).Add(x => x.TestId, "share"));

        var label = cut.Find("[data-testid=share]").GetAttribute("aria-label");
        Assert.Multiple(() =>
        {
            Assert.That(label, Does.Contain(first));
            Assert.That(label, Does.Contain(rest));
        });
    }

    [Test]
    public void Render_OneMacroZero_RendersOnlyTheOtherSegments()
    {
        var cut = Context.Render<EnergyShareBar>(p => p
            .Add(x => x.Protein, 10).Add(x => x.Carbs, 0).Add(x => x.Fat, 10).Add(x => x.TestId, "share"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=share-carbs-segment]"), Is.Empty);
            Assert.That(cut.FindAll(".segment"), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Render_TinyTotal_StillFillsTheBar()
    {
        var cut = Context.Render<EnergyShareBar>(p => p
            .Add(x => x.Protein, 0.1).Add(x => x.TestId, "share"));

        Assert.That(cut.Find("[data-testid=share-protein-segment]").GetAttribute("style"), Does.Contain("flex-grow: 100"));
    }
}
