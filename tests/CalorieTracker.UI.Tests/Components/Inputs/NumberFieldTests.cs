using CalorieTracker.UI.Components.Inputs;

namespace CalorieTracker.UI.Tests.Components.Inputs;

public class NumberFieldTests : ComponentTestBase
{
    [Test]
    public void Renders_ANumericInputWithAHiddenLabelAndTheUnit()
    {
        var cut = Render(p => p.Add(x => x.Value, 150m));

        var input = cut.Find("[data-testid=num]");
        Assert.Multiple(() =>
        {
            Assert.That(input.GetAttribute("value"), Is.EqualTo("150"));
            Assert.That(input.GetAttribute("inputmode"), Is.EqualTo("decimal"));
            Assert.That(cut.Find(".visually-hidden").TextContent, Is.EqualTo("Amount"));
            Assert.That(cut.Find("[data-testid=num-unit]").TextContent, Is.EqualTo("g"));
            Assert.That(cut.FindAll("button"), Is.Empty);
            Assert.That(input.HasAttribute("aria-invalid"), Is.False);
            Assert.That(cut.FindAll("[role=alert]"), Is.Empty);
        });
    }

    [Test]
    public void Typing_RaisesValueChangedWithTheNumber()
    {
        var changed = new List<decimal?>();
        var cut = Render(p => p.Add(x => x.ValueChanged, (decimal? v) => changed.Add(v)));

        cut.Find("[data-testid=num]").Input("12.5");
        cut.Find("[data-testid=num]").Input("12,5");
        cut.Find("[data-testid=num]").Input("abc");
        cut.Find("[data-testid=num]").Input("");

        Assert.That(changed, Is.EqualTo(new decimal?[] { 12.5m, 12.5m, null, null }));
    }

    [Test]
    public void Typing_KeepsTheTypedTextWhileTheParentTakesTheNumber()
    {
        decimal? value = null;
        var cut = Context.Render<NumberField>(p => p
            .Add(x => x.Label, "Amount")
            .Add(x => x.TestId, "num")
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, (decimal? v) => value = v));

        cut.Find("[data-testid=num]").Input("1.");
        cut.Render(p => p.Add(x => x.Value, value));

        Assert.That(cut.Find("[data-testid=num]").GetAttribute("value"), Is.EqualTo("1."));
    }

    [Test]
    public void Value_SetByTheParent_ReplacesTheText()
    {
        var cut = Render(p => p.Add(x => x.Value, 5m));

        cut.Render(p => p.Add(x => x.Value, 7m));

        Assert.That(cut.Find("[data-testid=num]").GetAttribute("value"), Is.EqualTo("7"));
    }

    [Test]
    public void Stepper_ShowsBothButtonsWithNamesAndTestIds()
    {
        var cut = Render(p => p.Add(x => x.Stepper, true).Add(x => x.Value, 50m));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=num-minus]").GetAttribute("aria-label"), Is.EqualTo("Decrease Amount"));
            Assert.That(cut.Find("[data-testid=num-plus]").GetAttribute("aria-label"), Is.EqualTo("Increase Amount"));
        });
    }

    [Test]
    public void Stepper_RaisesValueChangedByTheStep()
    {
        var changed = new List<decimal?>();
        var cut = Render(p => p
            .Add(x => x.Stepper, true)
            .Add(x => x.Step, 10m)
            .Add(x => x.Value, 150m)
            .Add(x => x.ValueChanged, (decimal? v) => changed.Add(v)));

        cut.Find("[data-testid=num-plus]").Click();
        cut.Find("[data-testid=num-minus]").Click();

        Assert.That(changed, Is.EqualTo(new decimal?[] { 160m, 140m }));
    }

    [Test]
    public void Stepper_FromEmpty_StartsAtTheMinimumOrZero()
    {
        var changed = new List<decimal?>();
        var cut = Render(p => p
            .Add(x => x.Stepper, true)
            .Add(x => x.Minimum, 1m)
            .Add(x => x.ValueChanged, (decimal? v) => changed.Add(v)));

        cut.Find("[data-testid=num-plus]").Click();

        Assert.That(changed, Is.EqualTo(new decimal?[] { 2m }));
    }

    [Test]
    public void Stepper_NeverGoesBelowTheMinimum()
    {
        var changed = new List<decimal?>();
        var cut = Render(p => p
            .Add(x => x.Stepper, true)
            .Add(x => x.Step, 10m)
            .Add(x => x.Minimum, 5m)
            .Add(x => x.Value, 12m)
            .Add(x => x.ValueChanged, (decimal? v) => changed.Add(v)));

        cut.Find("[data-testid=num-minus]").Click();

        Assert.That(changed, Is.EqualTo(new decimal?[] { 5m }));
    }

    [Test]
    public void Stepper_AtTheMinimum_DisablesMinusOnly()
    {
        var cut = Render(p => p.Add(x => x.Stepper, true).Add(x => x.Minimum, 5m).Add(x => x.Value, 5m));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=num-minus]").HasAttribute("disabled"), Is.True);
            Assert.That(cut.Find("[data-testid=num-plus]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public void Invalid_ShowsTheMessageAnnouncedAndLinkedToTheInput()
    {
        var cut = Render(p => p.Add(x => x.InvalidMessage, "Enter 10 g or more."));

        var message = cut.Find("[data-testid=num-error]");
        var input = cut.Find("[data-testid=num]");
        Assert.Multiple(() =>
        {
            Assert.That(message.TextContent, Is.EqualTo("Enter 10 g or more."));
            Assert.That(message.GetAttribute("role"), Is.EqualTo("alert"));
            Assert.That(message.ClassList, Does.Contain("message"));
            Assert.That(input.GetAttribute("aria-invalid"), Is.EqualTo("true"));
            Assert.That(input.GetAttribute("aria-describedby"), Is.EqualTo(message.Id));
            Assert.That(message.Id, Is.Not.Empty);
        });
    }

    [Test]
    public void Disabled_DisablesTheInputAndBothButtons()
    {
        var cut = Render(p => p.Add(x => x.Stepper, true).Add(x => x.Disabled, true).Add(x => x.Value, 50m));

        Assert.That(cut.FindAll("input, button").Select(e => e.HasAttribute("disabled")), Is.EqualTo(new[] { true, true, true }));
    }

    [Test]
    public void InteractiveElements_AllHaveTestIds()
    {
        var cut = Render(p => p.Add(x => x.Stepper, true));

        Assert.That(Stories.TestIdTests.WithoutTestId(cut.FindAll("*")), Is.Empty);
    }

    [Test]
    public void Css_SizesTheInputAndButtonsByTheTouchTargetAndShowsErrorsInTheErrorTone()
    {
        var css = File.ReadAllText(RepoFiles.Find("NumberField.razor.css", "src", "CalorieTracker.UI").Single());

        Assert.Multiple(() =>
        {
            Assert.That(css.Split(".step {")[1].Split('}')[0], Does.Contain("min-width: var(--touch-target)").And.Contain("min-height: var(--touch-target)"));
            Assert.That(css.Split(".input {")[1].Split('}')[0], Does.Contain("min-height: var(--touch-target)"));
            Assert.That(css.Split(".invalid .box {")[1].Split('}')[0], Does.Contain("var(--color-error)"));
            Assert.That(css.Split(".message {")[1].Split('}')[0], Does.Contain("color: var(--color-error)"));
        });
    }

    private IRenderedComponent<NumberField> Render(Action<ComponentParameterCollectionBuilder<NumberField>>? configure = null) =>
        Context.Render<NumberField>(p =>
        {
            p.Add(x => x.Label, "Amount").Add(x => x.Unit, "g").Add(x => x.TestId, "num");
            configure?.Invoke(p);
        });
}
