using CalorieTracker.UI.Components.Charts;

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
}
