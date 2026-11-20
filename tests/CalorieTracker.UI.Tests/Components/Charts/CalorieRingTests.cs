using CalorieTracker.UI.Components.Charts;
using CalorieTracker.UI.Tests.DesignSystem;

namespace CalorieTracker.UI.Tests.Components.Charts;

public class CalorieRingTests : ComponentTestBase
{
    [TestCase(0, 2100, "2,100", "kcal left", "0", "2,100 kcal left. Eaten 0 of 2,100 kcal target.")]
    [TestCase(659, 2100, "1,441", "kcal left", "0.3138", "1,441 kcal left. Eaten 659 of 2,100 kcal target.")]
    [TestCase(2100, 2100, "0", "kcal left", "1", "0 kcal left. Eaten 2,100 of 2,100 kcal target.")]
    [TestCase(2359, 2100, "259", "kcal over", "1", "259 kcal over. Eaten 2,359 of 2,100 kcal target.")]
    public void Render_WithTarget_ShowsLeftOrOver(double eaten, int target, string value, string label, string fill, string description)
    {
        var cut = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, eaten).Add(x => x.Target, target).Add(x => x.TestId, "ring"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=ring-value]").TextContent, Is.EqualTo(value));
            Assert.That(cut.Find("[data-testid=ring-label]").TextContent, Is.EqualTo(label));
            Assert.That(cut.Find("[data-testid=ring]").GetAttribute("aria-label"), Is.EqualTo(description));
            Assert.That(cut.Find("[data-testid=ring]").GetAttribute("style"), Does.Contain($"--fill: {fill}"));
        });
    }

    [TestCase(null)]
    [TestCase(0)]
    public void Render_WithNoTarget_ShowsCaloriesEaten(int? target)
    {
        var cut = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, 659).Add(x => x.Target, target).Add(x => x.TestId, "ring"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=ring-value]").TextContent, Is.EqualTo("659"));
            Assert.That(cut.Find("[data-testid=ring-label]").TextContent, Is.EqualTo("kcal eaten"));
            Assert.That(cut.Find("[data-testid=ring]").GetAttribute("aria-label"), Is.EqualTo("659 kcal eaten. No target set."));
        });
    }

    [TestCase(659, "1", "659")]
    [TestCase(0, "0", "0")]
    public void Render_WithNoTarget_FillsOnlyOnceSomethingIsEaten(double eaten, string fill, string value)
    {
        var cut = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, eaten).Add(x => x.TestId, "ring"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=ring]").GetAttribute("style"), Does.Contain($"--fill: {fill}"));
            Assert.That(cut.Find("[data-testid=ring-value]").TextContent, Is.EqualTo(value));
        });
    }

    [Test]
    public void Render_FractionalEaten_RoundsToTheNearestKcal()
    {
        var cut = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, 659.5).Add(x => x.Target, 2100).Add(x => x.TestId, "ring"));

        Assert.That(cut.Find("[data-testid=ring]").GetAttribute("aria-label"), Does.Contain("Eaten 660 of"));
    }

    [Test]
    public void Render_NegativeEaten_NeverShowsMoreThanTheTargetLeft()
    {
        var cut = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, -50).Add(x => x.Target, 2100).Add(x => x.TestId, "ring"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=ring-value]").TextContent, Is.EqualTo("2,100"));
            Assert.That(cut.Find("[data-testid=ring]").GetAttribute("style"), Does.Contain("--fill: 0"));
        });
    }

    [Test]
    public void Render_OverTarget_UsesTheErrorTone()
    {
        var over = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, 2359).Add(x => x.Target, 2100).Add(x => x.TestId, "ring"));
        var at = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, 2100).Add(x => x.Target, 2100).Add(x => x.TestId, "ring"));

        Assert.Multiple(() =>
        {
            Assert.That(over.Find("[data-testid=ring]").ClassList, Does.Contain("over"));
            Assert.That(at.Find("[data-testid=ring]").ClassList, Does.Not.Contain("over"));
        });
    }

    [Test]
    public void Compact_UsesTheCompactTokens()
    {
        var css = RepoFiles.Read("src", "CalorieTracker.UI", "Components", "Charts", "CalorieRing.razor.css");
        var tokens = AppTokens.AppCss;
        var normal = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, 100).Add(x => x.TestId, "ring"));
        var compact = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, 100).Add(x => x.Compact, true).Add(x => x.TestId, "ring"));

        Assert.Multiple(() =>
        {
            Assert.That(normal.Find("[data-testid=ring]").ClassList, Does.Not.Contain("compact"));
            Assert.That(compact.Find("[data-testid=ring]").ClassList, Does.Contain("compact"));
            Assert.That(css.Split(".compact {")[1].Split('}')[0], Does.Contain("--size: var(--ring-size-compact)").And.Contain("--stroke: var(--ring-width-compact)"));
            Assert.That(tokens, Does.Contain("--ring-size-compact: 6rem;").And.Contain("--ring-width-compact:"));
        });
    }

    [TestCase(999, "")]
    [TestCase(1234, "")]
    [TestCase(12345, "wider")]
    [TestCase(123456, "widest")]
    public void Render_StepsTheNumberDownByWidth(int eaten, string expected)
    {
        var cut = Context.Render<CalorieRing>(p => p.Add(x => x.Eaten, eaten).Add(x => x.TestId, "ring"));
        var classes = cut.Find("[data-testid=ring-value]").ClassList.Where(c => c != "value" && !c.StartsWith("b-", StringComparison.Ordinal));

        Assert.That(string.Join(" ", classes), Is.EqualTo(expected));
    }
}
