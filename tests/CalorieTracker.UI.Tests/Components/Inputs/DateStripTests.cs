using System.Text.RegularExpressions;
using CalorieTracker.UI.Components.Inputs;
using CalorieTracker.UI.Tests.DesignSystem;

namespace CalorieTracker.UI.Tests.Components.Inputs;

public class DateStripTests : ComponentTestBase
{
    private static readonly DateOnly Today = new(2026, 11, 15);

    [TestCase(0, "Today, 15 Nov")]
    [TestCase(-1, "Yesterday, 14 Nov")]
    [TestCase(1, "Tomorrow, 16 Nov")]
    [TestCase(-30, "Fri, 16 Oct")]
    [TestCase(110, "Fri, 5 Mar 2027")]
    public void Label_NamesTheDay(int offset, string expected)
    {
        var cut = Render(Today.AddDays(offset));

        Assert.That(cut.Find("[data-testid=date-strip-label]").TextContent.Trim(), Is.EqualTo(expected));
    }

    [Test]
    public void Buttons_HaveAccessibleNames()
    {
        var cut = Render(Today.AddDays(-1));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=date-strip-previous]").GetAttribute("aria-label"), Is.EqualTo("Previous day"));
            Assert.That(cut.Find("[data-testid=date-strip-next]").GetAttribute("aria-label"), Is.EqualTo("Next day"));
            Assert.That(cut.Find("[data-testid=date-strip-label]").GetAttribute("aria-label"), Is.EqualTo("Yesterday, 14 Nov, go to today"));
        });
    }

    [Test]
    public void Targets_AreAtLeast48px()
    {
        var tokens = DesignTokens.KitTokensCss;
        var label = RepoFiles.Rule(CssWithoutComments("DateStrip.razor.css"), "label");
        var cut = Render(Today);

        Assert.Multiple(() =>
        {
            Assert.That(tokens, Does.Match(@"--touch-target\s*:\s*48px\s*;"));
            Assert.That(label, Does.Match(@"min-height\s*:\s*var\(--touch-target\)"));
            Assert.That(cut.FindAll("button.icon-btn"), Has.Count.EqualTo(2));
            Assert.That(cut.FindAll("button.icon-btn.compact"), Is.Empty);
            Assert.That(cut.Find("button.label"), Is.Not.Null);
        });
    }

    [Test]
    public void Label_WrapsInsteadOfTruncating()
    {
        var strip = CssWithoutComments("DateStrip.razor.css");
        var label = RepoFiles.Rule(strip, "label");
        var cut = Render(Today);

        Assert.Multiple(() =>
        {
            Assert.That(strip, Does.Not.Contain("text-overflow"));
            Assert.That(strip, Does.Not.Match(@"white-space\s*:\s*nowrap"));
            Assert.That(label, Does.Match(@"overflow-wrap\s*:\s*break-word"));
            Assert.That(cut.Find("button.label").HasAttribute("data-wrap-ok"), Is.True);
        });
    }

    [Test]
    public void Label_OnNewYearsDay_NamesTomorrowWithItsYear()
    {
        var cut = Context.Render<DateStrip>(p => p
            .Add(x => x.Date, new DateOnly(2027, 1, 1))
            .Add(x => x.Today, new DateOnly(2026, 12, 31)));

        Assert.That(cut.Find("[data-testid=date-strip-label]").TextContent.Trim(), Is.EqualTo("Tomorrow, 1 Jan 2027"));
    }

    [Test]
    public void Label_ForAPastYear_ShowsTheYearBeforeAnyClick()
    {
        var cut = Render(new DateOnly(2025, 10, 11));

        Assert.That(cut.Find("[data-testid=date-strip-label]").TextContent.Trim(), Is.EqualTo("Sat, 11 Oct 2025"));
    }

    [Test]
    public void Previous_RaisesOnPrevious()
    {
        var raised = 0;
        var cut = Render(Today, onPrevious: () => raised++);

        cut.Find("[data-testid=date-strip-previous]").Click();

        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void Next_RaisesOnNext()
    {
        var raised = 0;
        var cut = Render(Today, onNext: () => raised++);

        cut.Find("[data-testid=date-strip-next]").Click();

        Assert.That(raised, Is.EqualTo(1));
    }

    [TestCase(-1)]
    [TestCase(1)]
    [TestCase(-400)]
    [TestCase(900)]
    public void TappingTheLabel_FromAnyDay_ReturnsToToday(int offset)
    {
        var shown = Today.AddDays(offset);
        var cut = Render(shown, onToday: () => shown = Today);

        cut.Find("[data-testid=date-strip-label]").Click();
        cut.Render(p => p.Add(x => x.Date, shown));

        Assert.That(cut.Find("[data-testid=date-strip-label]").TextContent.Trim(), Is.EqualTo("Today, 15 Nov"));
    }

    [Test]
    public void TestId_PrefixesEveryTestId()
    {
        var cut = Context.Render<DateStrip>(p => p
            .Add(x => x.Date, Today)
            .Add(x => x.Today, Today)
            .Add(x => x.TestId, "diary-date"));

        Assert.That(cut.FindAll("[data-testid]").Select(e => e.GetAttribute("data-testid")),
            Is.EqualTo(new[] { "diary-date", "diary-date-previous", "diary-date-label", "diary-date-next" }));
    }

    [Test]
    public void Stories_AreTodayYesterdayTomorrowAndFarDate()
    {
        var stories = File.ReadAllText(RepoFiles.Find("DateStrip.stories.razor", "src", "CalorieTracker.UI.Stories").Single());

        var names = Regex.Matches(stories, @"<Story\s+Name=""(?<name>[^""]+)""").Select(m => m.Groups["name"].Value);

        Assert.That(names, Is.EqualTo(new[] { "Today", "Yesterday", "Tomorrow", "Far date" }));
    }

    private static string CssWithoutComments(string file) =>
        Regex.Replace(File.ReadAllText(RepoFiles.Find(file, "src", "CalorieTracker.UI").Single()), @"/\*.*?\*/", "", RegexOptions.Singleline);

    private IRenderedComponent<DateStrip> Render(DateOnly date, Action? onPrevious = null, Action? onNext = null, Action? onToday = null) =>
        Context.Render<DateStrip>(p => p
            .Add(x => x.Date, date)
            .Add(x => x.Today, Today)
            .Add(x => x.OnPrevious, () => onPrevious?.Invoke())
            .Add(x => x.OnNext, () => onNext?.Invoke())
            .Add(x => x.OnToday, () => onToday?.Invoke()));
}
