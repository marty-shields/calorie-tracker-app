using System.Globalization;
using AngleSharp.Dom;
using CalorieTracker.UI.Components.Charts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CalorieTracker.UI.Tests.Components.Charts;

public class TrendBarChartTests : ComponentTestBase
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private readonly List<DateOnly> selected = [];

    [SetUp]
    public void ClearSelection() => selected.Clear();

    private static TrendBar Bar(int day, double? value, string? label = null) =>
        new(Monday.AddDays(day), label ?? Monday.AddDays(day).ToString("ddd", CultureInfo.InvariantCulture), value);

    private IRenderedComponent<TrendBarChart> Render(IReadOnlyList<TrendBar> bars, double? target = 2000) =>
        Context.Render<TrendBarChart>(p => p
            .Add(x => x.Bars, bars).Add(x => x.Target, target).Add(x => x.TestId, "chart")
            .Add(x => x.OnSelect, EventCallback.Factory.Create<DateOnly>(this, selected.Add)));

    private static double Number(IElement element, string attribute) =>
        double.Parse(element.GetAttribute(attribute)!, CultureInfo.InvariantCulture);

    private static double Height(IRenderedComponent<TrendBarChart> cut, int day) =>
        Number(cut.Find($"[data-testid=chart-column-{Monday.AddDays(day):yyyy-MM-dd}]"), "data-height");

    [Test]
    public void Render_ScalesBarHeightsWithValuesOnOneYScale()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        var (small, tall) = (Height(cut, 0), Height(cut, 1));
        Assert.Multiple(() =>
        {
            Assert.That(small, Is.GreaterThan(0));
            Assert.That(tall / small, Is.EqualTo(2).Within(0.02));
        });
    }

    [Test]
    public void Render_HeightsFollowTheTicks()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        var baseline = Number(cut.Find("[data-testid=chart-baseline]"), "y1");
        var tickY = (int kcal) => Number(cut.Find($"[data-testid=chart-grid-{kcal}]"), "y1");
        Assert.Multiple(() =>
        {
            Assert.That(baseline - tickY(1000), Is.EqualTo(Height(cut, 0)).Within(0.02));
            Assert.That(baseline - tickY(2000), Is.EqualTo(Height(cut, 1)).Within(0.02));
        });
    }

    [Test]
    public void Render_BandEdgesSitAtNinetyAndOneHundredTenPercentOfTarget()
    {
        // Bars of exactly 90% and 110% of the 2,000 target mark where the band's bottom and top edges should be.
        var cut = Render([Bar(0, 1800), Bar(1, 2200)]);

        var band = cut.Find("[data-testid=chart-band]");
        var baseline = Number(cut.Find("[data-testid=chart-baseline]"), "y1");
        var top = Number(band, "y");
        var bottom = top + Number(band, "height");
        Assert.Multiple(() =>
        {
            Assert.That(baseline - top, Is.EqualTo(Height(cut, 1)).Within(0.02));
            Assert.That(baseline - bottom, Is.EqualTo(Height(cut, 0)).Within(0.02));
        });
    }

    [Test]
    public void Render_DrawsADashedTargetLineAtTheTarget()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        var line = cut.Find("[data-testid=chart-target]");
        var baseline = Number(cut.Find("[data-testid=chart-baseline]"), "y1");
        Assert.Multiple(() =>
        {
            Assert.That(line.ClassList, Does.Contain("target"));
            Assert.That(baseline - Number(line, "y1"), Is.EqualTo(Height(cut, 1)).Within(0.02));
        });
    }

    [Test]
    public void Render_WithoutATarget_DrawsNoBandOrLineAndNoStatus()
    {
        var cut = Render([Bar(0, 1000)], target: null);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=chart-band]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=chart-target]"), Is.Empty);
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09]").GetAttribute("aria-label"), Is.EqualTo("Mon 9 Nov, 1,000 kcal"));
        });
    }

    [TestCase(1000, "under", "under target")]
    [TestCase(1799, "under", "under target")]
    [TestCase(1800, "on", "on target")]
    [TestCase(2200, "on", "on target")]
    [TestCase(2201, "over", "over target")]
    public void Render_GivesEachBarAStatusClassAndAccessibleName(double value, string statusClass, string word)
    {
        var cut = Render([Bar(0, value)]);

        var bar = cut.Find("[data-testid=chart-bar-2026-11-09]");
        Assert.Multiple(() =>
        {
            Assert.That(bar.ClassList, Does.Contain(statusClass));
            Assert.That(bar.GetAttribute("aria-label"),
                Is.EqualTo($"Mon 9 Nov, {value.ToString("N0", CultureInfo.InvariantCulture)} kcal, {word}"));
        });
    }

    [Test]
    public void Render_JudgesABarAgainstItsOwnTarget()
    {
        var cut = Render([new TrendBar(Monday, "Mon", 2500, Target: 2400), Bar(1, 2500)], target: 2000);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09]").ClassList, Does.Contain("on"));
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-10]").ClassList, Does.Contain("over"));
        });
    }

    [Test]
    public void Render_MarksOverBarsWithMoreThanColour()
    {
        var cut = Render([Bar(0, 2500), Bar(1, 2000), Bar(2, 1000)]);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=chart-marker-2026-11-09]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=chart-marker-2026-11-10]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=chart-marker-2026-11-11]"), Is.Empty);
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09]").GetAttribute("aria-label"), Does.Contain("over target"));
        });
    }

    [Test]
    public void Render_Bars_AreFocusableButtons()
    {
        var cut = Render([Bar(0, 1000), Bar(1, null), Bar(2, 2000)]);

        var bars = cut.FindAll("g.bar");
        Assert.Multiple(() =>
        {
            Assert.That(bars, Has.Count.EqualTo(2));
            Assert.That(bars.Select(b => b.GetAttribute("role")), Is.All.EqualTo("button"));
            Assert.That(bars.Select(b => b.GetAttribute("tabindex")), Is.All.EqualTo("0"));
        });
    }

    [Test]
    public void Click_RaisesOnSelectWithTheBarsDate()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        cut.Find("[data-testid=chart-bar-2026-11-10]").Click();

        Assert.That(selected, Is.EqualTo(new[] { new DateOnly(2026, 11, 10) }));
    }

    [TestCase("Enter")]
    [TestCase(" ")]
    public void Key_EnterOrSpace_RaisesOnSelectWithTheBarsDate(string key)
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        cut.Find("[data-testid=chart-bar-2026-11-09]").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.That(selected, Is.EqualTo(new[] { new DateOnly(2026, 11, 9) }));
    }

    [Test]
    public void Key_Other_DoesNothing()
    {
        var cut = Render([Bar(0, 1000)]);

        cut.Find("[data-testid=chart-bar-2026-11-09]").KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.That(selected, Is.Empty);
    }

    [Test]
    public void Render_LabelsOnlyTheHighestBar()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2345), Bar(2, 1500)]);

        var labels = cut.FindAll("[data-testid=chart-value-label]");
        Assert.Multiple(() =>
        {
            Assert.That(labels, Has.Count.EqualTo(1));
            Assert.That(labels[0].TextContent, Is.EqualTo("2,345"));
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-10]").QuerySelector("[data-testid=chart-value-label]"), Is.Not.Null);
        });
    }

    [TestCase(1000, 1500, new[] { "0", "1k", "2k" })]
    [TestCase(2000, 2000, new[] { "0", "1k", "2k", "3k" })]
    [TestCase(3000, 2500, new[] { "0", "1k", "2k", "3k" })]
    [TestCase(3100, 2000, new[] { "0", "1k", "2k", "3k", "4k" })]
    public void Render_ShowsYTicksFromZeroAndAThousandApart(double tallest, double target, string[] expected)
    {
        // A 2,000 target puts the band's top at 2,200, which needs the 3k tick.
        var cut = Render([Bar(0, tallest)], target);

        Assert.That(cut.FindAll("text.tick").Select(t => t.TextContent), Is.EqualTo(expected));
    }

    [Test]
    public void Render_WithoutATargetAndLowBars_StopsAtTwoThousand()
    {
        var cut = Render([Bar(0, 800)], target: null);

        Assert.That(cut.FindAll("text.tick").Select(t => t.TextContent), Is.EqualTo(new[] { "0", "1k", "2k" }));
    }

    [Test]
    public void Render_MissingValue_LeavesAnEmptySlot()
    {
        var cut = Render([Bar(0, 1000), Bar(1, null), Bar(2, 0), Bar(3, 1500)]);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("g.bar"), Has.Count.EqualTo(2));
            Assert.That(cut.FindAll("[data-testid=chart-gap-2026-11-10]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=chart-gap-2026-11-11]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=chart-column-2026-11-10]"), Is.Empty);
            Assert.That(cut.FindAll("text.axis-label").Select(t => t.TextContent), Is.EqualTo(new[] { "Mon", "Tue", "Wed", "Thu" }));
        });
    }

    [Test]
    public void Render_EmptyChart_KeepsAxesAndSaysSo()
    {
        var cut = Render([Bar(0, null), Bar(1, null)]);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=chart-empty]").TextContent, Is.EqualTo("No days logged yet"));
            Assert.That(cut.FindAll("g.bar"), Is.Empty);
            Assert.That(cut.FindAll("text.tick"), Is.Not.Empty);
        });
    }

    [Test]
    public void Render_TooltipHoldsTheAccessibleNameForHoverAndFocus()
    {
        var cut = Render([Bar(0, 2500)]);

        Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09] text.tip").TextContent, Is.EqualTo("Mon 9 Nov, 2,500 kcal, over target"));
    }

    [Test]
    public void Render_LongLabels_AreShortenedUnderTheBarButKeptInTheName()
    {
        var cut = Render(Enumerable.Range(0, 7).Select(i => Bar(i, 1000, "Monday afternoon")).ToList());

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("text.axis-label")[0].TextContent, Is.EqualTo("Mo…"));
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09]").GetAttribute("aria-label"), Does.StartWith("Mon 9 Nov"));
        });
    }

    [Test]
    public void Render_TwelveBars_ShowOneLetterEach()
    {
        var cut = Render(Enumerable.Range(0, 12).Select(i => Bar(i, 1000, "Mar")).ToList());

        Assert.That(cut.FindAll("text.axis-label").Select(t => t.TextContent), Has.All.EqualTo("M"));
    }

    [Test]
    public void Render_DateFormatShapesTheName()
    {
        var cut = Context.Render<TrendBarChart>(p => p
            .Add(x => x.Bars, [new TrendBar(new DateOnly(2026, 3, 1), "Mar", 1500)]).Add(x => x.DateFormat, "MMMM yyyy").Add(x => x.TestId, "chart"));

        Assert.That(cut.Find("g.bar").GetAttribute("aria-label"), Is.EqualTo("March 2026, 1,500 kcal"));
    }

    [Test]
    public void Render_NoBarsAtAll_DoesNotThrow()
    {
        var cut = Render([]);

        Assert.That(cut.Find("[data-testid=chart-empty]"), Is.Not.Null);
    }
}
