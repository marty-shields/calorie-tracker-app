namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The App has no _Imports.razor, so a page that uses @onclick needs its own using, or the attribute renders as plain text and never fires.</summary>
public class AppMarkupEventsTests
{
    private static IEnumerable<TestCaseData> AppPages() =>
        RepoFiles.Find("*.razor", "src", "CalorieTracker.App")
            .Where(f => Path.GetFileName(f) != "_Imports.razor")
            .Select(f => new TestCaseData(f).SetArgDisplayNames(Path.GetFileName(f)));

    [TestCaseSource(nameof(AppPages))]
    public void PageWithEventHandlers_ImportsTheWebEvents(string file)
    {
        var markup = File.ReadAllText(file);
        var usesEvents = markup.Contains(" @onclick", StringComparison.Ordinal) || markup.Contains(" @oninput", StringComparison.Ordinal) || markup.Contains(" @onchange", StringComparison.Ordinal);
        if (!usesEvents)
        {
            Assert.Pass();
        }

        Assert.That(markup, Does.Contain("@using Microsoft.AspNetCore.Components.Web"));
    }
}
