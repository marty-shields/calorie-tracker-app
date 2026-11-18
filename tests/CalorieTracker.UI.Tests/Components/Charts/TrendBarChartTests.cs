using System.Globalization;
using System.Text.RegularExpressions;
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

    private static readonly Regex TopArc = new(@"A \S+ \S+ 0 0 1 \S+ (?<y>\S+)", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>The y of the top of the column as drawn in its path, which is where the first rounded corner ends.</summary>
    private static double DrawnTop(IRenderedComponent<TrendBarChart> cut, int day)
    {
        var d = cut.Find($"[data-testid=chart-column-{Monday.AddDays(day):yyyy-MM-dd}]").GetAttribute("d")!;
        return double.Parse(TopArc.Match(d).Groups["y"].Value, CultureInfo.InvariantCulture);
    }

    [Test]
    public void Render_DrawsEachColumnFromTheBaselineUpToItsValueWithARoundedTop()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        var baseline = Number(cut.Find("[data-testid=chart-baseline]"), "y1");
        var tickY = (int kcal) => Number(cut.Find($"[data-testid=chart-grid-{kcal}]"), "y1");
        Assert.Multiple(() =>
        {
            Assert.That(DrawnTop(cut, 0), Is.EqualTo(tickY(1000)).Within(0.02));
            Assert.That(DrawnTop(cut, 1), Is.EqualTo(tickY(2000)).Within(0.02));
            Assert.That(baseline - DrawnTop(cut, 1), Is.EqualTo(2 * (baseline - DrawnTop(cut, 0))).Within(0.05));
            Assert.That(cut.Find("[data-testid=chart-column-2026-11-09]").GetAttribute("d"), Does.Match(@"^M \S+ \S+ V \S+ A "));
        });
    }

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

    [TestCase(null)]
    [TestCase(0.0)]
    public void Render_WithoutATarget_DrawsNoBandOrLineAndNoStatus(double? target)
    {
        var cut = Render([Bar(0, 1000)], target);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=chart-band]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=chart-target]"), Is.Empty);
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09]").GetAttribute("aria-label"), Is.EqualTo("Mon 9 Nov, 1,000 kcal"));
            Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09]").ClassList, Does.Contain("plain"));
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
            Assert.That(bars.Select(b => b.ClassList.Contains("selectable")), Is.All.True);
        });
    }

    [Test]
    public void Click_RaisesOnSelectWithTheBarsDate()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        cut.Find("[data-testid=chart-bar-2026-11-10]").Click();

        Assert.That(selected, Is.EqualTo(new[] { new DateOnly(2026, 11, 10) }));
    }

    [Test]
    public void Key_Enter_RaisesOnSelectWithTheBarsDate()
    {
        var cut = Render([Bar(0, 1000), Bar(1, 2000)]);

        cut.Find("[data-testid=chart-bar-2026-11-09]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.That(selected, Is.EqualTo(new[] { new DateOnly(2026, 11, 9) }));
    }

    [Test]
    public void Key_Space_DoesNothingAsItWouldScrollThePage()
    {
        var cut = Render([Bar(0, 1000)]);

        cut.Find("[data-testid=chart-bar-2026-11-09]").KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.That(selected, Is.Empty);
    }

    [Test]
    public void Render_WithoutOnSelect_BarsAreNotInteractive()
    {
        var cut = Context.Render<TrendBarChart>(p => p.Add(x => x.Bars, [Bar(0, 1000), Bar(1, 2000)]).Add(x => x.Target, 2000).Add(x => x.TestId, "chart"));

        var bars = cut.FindAll("g.bar");
        Assert.Multiple(() =>
        {
            Assert.That(bars, Has.Count.EqualTo(2));
            Assert.That(bars.Select(b => b.GetAttribute("role")), Is.All.EqualTo("img"));
            Assert.That(bars.Select(b => b.HasAttribute("tabindex")), Is.All.False);
            Assert.That(bars.Select(b => b.ClassList.Contains("selectable")), Is.All.False);
            Assert.That(bars[0].GetAttribute("aria-label"), Is.EqualTo("Mon 9 Nov, 1,000 kcal, under target"));
        });
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
    [TestCase(4000, 2000, new[] { "0", "1k", "2k", "3k", "4k" })]
    [TestCase(4001, 2000, new[] { "0", "2k", "4k", "6k" })]
    [TestCase(6000, 2000, new[] { "0", "2k", "4k", "6k" })]
    [TestCase(9000, 2000, new[] { "0", "5k", "10k" })]
    [TestCase(74000, 2000, new[] { "0", "20k", "40k", "60k", "80k" })]
    public void Render_ShowsAFewNiceYTicksFromZero(double tallest, double target, string[] expected)
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
    public void Render_OutlierBar_StillFitsInsideTheChartWithFewTicks()
    {
        var cut = Render([Bar(0, 74000), Bar(1, 1500)]);

        var top = Number(cut.Find("[data-testid=chart-grid-80000]"), "y1");
        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("text.tick"), Has.Count.LessThanOrEqualTo(6));
            Assert.That(DrawnTop(cut, 0), Is.GreaterThan(top));
            Assert.That(top, Is.GreaterThanOrEqualTo(40), "room above the scale for the tooltip, the value and the over marker");
        });
    }

    [TestCase(0)]
    [TestCase(11)]
    public void Render_TooltipAtEitherEnd_StaysInsideTheChart(int index)
    {
        var bars = Enumerable.Range(0, 12).Select(i => new TrendBar(new DateOnly(2026, 1 + i, 1), "Month", i == 0 || i == 11 ? 1200 : 1000)).ToList();
        var cut = Context.Render<TrendBarChart>(p => p
            .Add(x => x.Bars, bars).Add(x => x.Target, 2000).Add(x => x.DateFormat, "MMMM yyyy").Add(x => x.TestId, "chart"));

        var tip = cut.FindAll("g.bar")[index].QuerySelector("text.tip")!;
        var half = tip.TextContent.Length * 9 / 2.0;
        var x = Number(tip, "x");
        Assert.Multiple(() =>
        {
            Assert.That(x - half, Is.GreaterThanOrEqualTo(0), "the text starts inside the left edge of the 320 wide chart");
            Assert.That(x + half, Is.LessThanOrEqualTo(320), "and ends inside the right edge");
            Assert.That(tip.GetAttribute("text-anchor"), Is.EqualTo("middle"));
        });
    }

    [Test]
    public void Render_TooltipOfAMiddleBar_SitsOverItsBar()
    {
        var cut = Render(Enumerable.Range(0, 7).Select(i => Bar(i, 1000)).ToList());

        var bar = cut.FindAll("g.bar")[3];
        var slot = (312 - 34) / 7.0;
        Assert.That(Number(bar.QuerySelector("text.tip")!, "x"), Is.EqualTo(34 + 3.5 * slot).Within(0.02));
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
    public void Render_TooltipHoldsTheDateAndValueForHoverAndFocus()
    {
        var cut = Render([Bar(0, 2500)]);

        Assert.That(cut.Find("[data-testid=chart-bar-2026-11-09] text.tip").TextContent, Is.EqualTo("Mon 9 Nov, 2,500 kcal"));
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
