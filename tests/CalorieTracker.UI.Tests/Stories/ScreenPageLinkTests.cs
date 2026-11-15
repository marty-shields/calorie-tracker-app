using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.Stories;

public sealed partial class ScreenPageLinkTests
{
    [GeneratedRegex(@"\?path=/story/(?<id>[^)\s""]+)")]
    private static partial Regex StoryLink();

    private static string StoryId(ScreenState state) =>
        $"{state.Section.Replace('/', '-')}--{state.Name.Replace(' ', '-')}".ToLowerInvariant();

    [Test]
    public void ScreenPages_LinkOnlyToStoriesThatExist()
    {
        var ids = ScreenStates.All.Select(StoryId).ToHashSet();
        var stale = RepoFiles.Find("*.md", "src", "CalorieTracker.UI.Stories", "Stories", "Screens")
            .SelectMany(file => StoryLink().Matches(File.ReadAllText(file))
                .Select(m => m.Groups["id"].Value)
                .Where(id => !ids.Contains(id))
                .Select(id => $"{Path.GetFileName(file)}: {id}"));

        Assert.That(stale, Is.Empty);
    }
}
