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

    private static IEnumerable<string> StoryNames(string component) =>
        StoryName().Matches(RepoFiles.Read("src", "CalorieTracker.UI.Stories", "Stories", "Components", "Charts", $"{component}.stories.razor"))
            .Select(m => m.Groups[1].Value);

    [GeneratedRegex("""<Story Name="([^"]+)">""")]
    private static partial Regex StoryName();
}
