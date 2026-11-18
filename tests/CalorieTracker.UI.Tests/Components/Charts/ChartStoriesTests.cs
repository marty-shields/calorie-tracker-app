using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.Components.Charts;

public partial class ChartStoriesTests
{
    [Test]
    public void CalorieRingStories_CoverEveryState() =>
        Assert.That(StoryNames("CalorieRing"),
            Is.EquivalentTo(new[] { "Empty", "Partial", "Exactly at target", "Over target", "No target" }));

    [Test]
    public void EnergyShareBarStories_CoverEveryState() =>
        Assert.That(StoryNames("EnergyShareBar"),
            Is.EquivalentTo(new[] { "Balanced", "One dominant", "All zero" }));

    [Test]
    public void TrendBarChartStories_CoverEveryState() =>
        Assert.That(StoryNames("TrendBarChart"),
            Is.EquivalentTo(new[] { "Week against target", "Year by month", "All over", "Single bar", "Empty", "Long labels", "No target" }));

    [Test]
    public void MonthCalendarStories_CoverEveryState() =>
        Assert.That(StoryNames("MonthCalendar"),
            Is.EquivalentTo(new[] { "Month in progress", "Finished month starting on Monday", "All over", "Nothing logged", "Single day logged" }));

    [Test]
    public void StatTileStories_CoverEveryState() =>
        Assert.That(StoryNames("StatTile"), Is.EquivalentTo(new[] { "Figure and caption", "Not enough days yet", "Long caption and figure" }));

    [Test]
    public void BarListStories_CoverEveryState() =>
        Assert.That(StoryNames("BarList"),
            Is.EquivalentTo(new[] { "By meal", "Days logged", "Long labels", "Single row", "Empty", "All zero" }));

    [Test]
    public void ChartLegendStories_CoverEveryState() =>
        Assert.That(StoryNames("ChartLegend"), Is.EquivalentTo(new[] { "Every entry", "Bar chart", "One entry", "Long labels" }));

    private static IEnumerable<string> StoryNames(string component) =>
        StoryName().Matches(RepoFiles.Read("src", "CalorieTracker.UI.Stories", "Stories", "Components", "Charts", $"{component}.stories.razor"))
            .Select(m => m.Groups[1].Value);

    [GeneratedRegex("""<Story Name="([^"]+)">""")]
    private static partial Regex StoryName();
}
