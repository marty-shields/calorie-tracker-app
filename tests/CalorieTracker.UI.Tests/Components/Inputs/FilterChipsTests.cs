using CalorieTracker.UI.Components.Inputs;

namespace CalorieTracker.UI.Tests.Components.Inputs;

public class FilterChipsTests : ComponentTestBase
{
    private static readonly IReadOnlyList<RadioOption<int>> Options =
    [
        new(1, "One", "one"),
        new(2, "Two", "two", "Not shown"),
        new(3, "Three", "three"),
    ];

    [Test]
    public void Renders_AHiddenLabelAndEachOptionWithItsTestId()
    {
        var cut = Render(value: 1);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("fieldset").GetAttribute("data-testid"), Is.EqualTo("chips"));
            Assert.That(cut.Find("legend").TextContent, Is.EqualTo("Choose"));
            Assert.That(cut.Find("legend").ClassList, Does.Contain("visually-hidden"));
            Assert.That(cut.FindAll(".text").Select(t => t.TextContent), Is.EqualTo(new[] { "One", "Two", "Three" }));
            Assert.That(cut.FindAll("input[type=radio]").Select(i => i.GetAttribute("data-testid")), Is.EqualTo(new[] { "one", "two", "three" }));
            Assert.That(cut.Markup, Does.Not.Contain("Not shown"));
        });
    }

    [Test]
    public void Value_ChecksItsOptionOnly()
    {
        var cut = Render(value: 2);

        Assert.That(cut.FindAll("input").Select(i => i.HasAttribute("checked")), Is.EqualTo(new[] { false, true, false }));
    }

    [Test]
    public void Options_ShareOneNamePerGroup()
    {
        var first = Render(value: 1);
        var second = Render(value: 1);

        var names = first.FindAll("input").Select(i => i.GetAttribute("name")).Distinct().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(names, Has.Count.EqualTo(1));
            Assert.That(second.Find("input").GetAttribute("name"), Is.Not.EqualTo(names[0]));
        });
    }

    [Test]
    public void TappingAChip_RaisesValueChangedWithItsValue()
    {
        var changed = new List<int>();
        var cut = Context.Render<FilterChips<int>>(p => p
            .Add(x => x.Label, "Choose")
            .Add(x => x.Options, Options)
            .Add(x => x.Value, 1)
            .Add(x => x.ValueChanged, (int v) => changed.Add(v)));

        cut.Find("[data-testid=three]").Change(true);

        Assert.That(changed, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void TappingAChip_TheParentDoesNotTake_RedrawsTheRadiosOnce()
    {
        var cut = Render(value: 1);
        var renders = cut.RenderCount;

        cut.Find("[data-testid=three]").Change(true);

        Assert.Multiple(() =>
        {
            Assert.That(cut.RenderCount - renders, Is.EqualTo(2));
            Assert.That(cut.FindAll("input").Select(i => i.HasAttribute("checked")), Is.EqualTo(new[] { true, false, false }));
        });
    }

    [Test]
    public void TappingAChip_TheParentTakes_DoesNotRedrawTheRadios()
    {
        var value = 1;
        var cut = Context.Render<FilterChips<int>>(p => p
            .Add(x => x.Label, "Choose")
            .Add(x => x.Options, Options)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, (int v) => value = v));
        cut.Render(p => p.Add(x => x.Value, 3));
        var renders = cut.RenderCount;

        cut.Find("[data-testid=three]").Change(true);

        Assert.That(cut.RenderCount - renders, Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void NullValue_ChecksTheOptionWhoseValueIsNull()
    {
        var cut = Context.Render<FilterChips<int?>>(p => p
            .Add(x => x.Label, "Choose")
            .Add(x => x.Options, [new RadioOption<int?>(1, "One", "one"), new RadioOption<int?>(null, "All", "all")])
            .Add(x => x.Value, null)
            .Add(x => x.ValueChanged, (int? _) => { }));

        Assert.That(cut.FindAll("input").Select(i => i.HasAttribute("checked")), Is.EqualTo(new[] { false, true }));
    }

    private IRenderedComponent<FilterChips<int>> Render(int value) =>
        Context.Render<FilterChips<int>>(p => p
            .Add(x => x.Label, "Choose")
            .Add(x => x.Options, Options)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, (int _) => { })
            .Add(x => x.TestId, "chips"));
}
