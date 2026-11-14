using CalorieTracker.UI.Components.Lists;

namespace CalorieTracker.UI.Tests.Components.Lists;

public class FoodValuesTests : ComponentTestBase
{
    private IRenderedComponent<FoodValues> Render(Action<ComponentParameterCollectionBuilder<FoodValues>>? more = null) =>
        Context.Render<FoodValues>(p =>
        {
            p.Add(x => x.Name, "Porridge oats")
                .Add(x => x.Kcal, 372)
                .Add(x => x.Protein, 11)
                .Add(x => x.Carbs, 60.04)
                .Add(x => x.Fat, 8.5)
                .Add(x => x.TestId, "food");
            more?.Invoke(p);
        });

    private static string Text(IRenderedComponent<FoodValues> cut, string part) => cut.Find($"[data-testid=food-{part}]").TextContent;

    [Test]
    public void Render_ShowsNameAndTheFourValuesPer100g()
    {
        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "name"), Is.EqualTo("Porridge oats"));
            Assert.That(Text(cut, "per"), Is.EqualTo("Per 100 g"));
            Assert.That(Text(cut, "kcal"), Is.EqualTo("372 kcal"));
            Assert.That(Text(cut, "protein"), Is.EqualTo("11 g"));
            Assert.That(Text(cut, "carbs"), Is.EqualTo("60 g"));
            Assert.That(Text(cut, "fat"), Is.EqualTo("8.5 g"));
            Assert.That(cut.FindAll("dt").Select(e => e.TextContent), Is.EqualTo(new[] { "Energy", "Protein", "Carbs", "Fat" }));
            Assert.That(cut.FindAll("[data-testid=food-brand]"), Is.Empty);
        });
    }

    [Test]
    public void Render_WithBrandAndLiquid_ShowsBrandAndPer100Ml()
    {
        var cut = Render(p => p.Add(x => x.Brand, "Morning Mill").Add(x => x.IsLiquid, true));

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "brand"), Is.EqualTo("Morning Mill"));
            Assert.That(Text(cut, "per"), Is.EqualTo("Per 100 ml"));
        });
    }

    [Test]
    public void Render_WithExtraNutrients_ShowsEachRowInOrder()
    {
        var cut = Render(p => p.Add(x => x.Fibre, 9).Add(x => x.Sugars, 1.1).Add(x => x.Saturates, 1.4).Add(x => x.Salt, 0.3));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("dt").Select(e => e.TextContent), Is.EqualTo(new[] { "Energy", "Protein", "Carbs", "Fat", "Fibre", "Sugars", "Saturates", "Salt" }));
            Assert.That(Text(cut, "fibre"), Is.EqualTo("9 g"));
            Assert.That(Text(cut, "salt"), Is.EqualTo("0.3 g"));
        });
    }

    [Test]
    public void Render_WithOnlyOneExtraNutrient_ShowsOnlyThatRow()
    {
        var cut = Render(p => p.Add(x => x.Sugars, 0));

        Assert.That(cut.FindAll("dt").Select(e => e.TextContent), Does.Contain("Sugars").And.Not.Contain("Fibre"));
        Assert.That(Text(cut, "sugars"), Is.EqualTo("0 g"));
    }

    [Test]
    public void Render_ShowsContentAfterTheRows()
    {
        var cut = Render(p => p.AddChildContent("<p class=\"extra\">Check the label</p>"));

        Assert.That(cut.Find(".values + .extra").TextContent, Is.EqualTo("Check the label"));
    }
}
