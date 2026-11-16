using CalorieTracker.UI.Components.Inputs;

namespace CalorieTracker.UI.Tests.Components.Inputs;

public class SelectFieldTests : ComponentTestBase
{
    private static readonly IReadOnlyList<RadioOption<int>> Options =
    [
        new(10, "Glass", "glass"),
        new(20, "Can", "can"),
        new(30, "Bottle", "bottle"),
    ];

    private IRenderedComponent<SelectField<int>> Render(int value = 20, Action<int>? changed = null, bool disabled = false) =>
        Context.Render<SelectField<int>>(p => p
            .Add(x => x.Label, "Serving")
            .Add(x => x.Options, Options)
            .Add(x => x.Value, value)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.TestId, "pick")
            .Add(x => x.ValueChanged, changed ?? (_ => { })));

    [Test]
    public void Renders_ASelectWithAHiddenLabelAndEachOptionInOrder()
    {
        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("select").GetAttribute("data-testid"), Is.EqualTo("pick"));
            Assert.That(cut.Find(".visually-hidden").TextContent, Is.EqualTo("Serving"));
            Assert.That(cut.FindAll("option").Select(o => o.TextContent), Is.EqualTo(new[] { "Glass", "Can", "Bottle" }));
        });
    }

    [Test]
    public void Value_SelectsItsOptionOnly() =>
        Assert.That(Render(30).FindAll("option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { false, false, true }));

    [Test]
    public void Picking_RaisesTheValueOfThatOption()
    {
        var picked = new List<int>();
        var cut = Render(10, picked.Add);

        cut.Find("select").Change("2");
        cut.Find("select").Change("0");

        Assert.That(picked, Is.EqualTo(new[] { 30, 10 }));
    }

    [TestCase("")]
    [TestCase("x")]
    [TestCase("-1")]
    [TestCase("3")]
    public void Picking_SomethingThatIsNoOption_RaisesNothing(string value)
    {
        var picked = new List<int>();
        var cut = Render(10, picked.Add);

        cut.Find("select").Change(value);

        Assert.That(picked, Is.Empty);
    }

    [Test]
    public void Disabled_DisablesTheSelect()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Render(disabled: true).Find("select").HasAttribute("disabled"), Is.True);
            Assert.That(Render().Find("select").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public void ValueChangedNotTakenByTheParent_RedrawsSoTheChosenOptionStays()
    {
        var cut = Render(10);

        cut.Find("select").Change("1");

        Assert.That(cut.FindAll("option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { true, false, false }));
    }
}
