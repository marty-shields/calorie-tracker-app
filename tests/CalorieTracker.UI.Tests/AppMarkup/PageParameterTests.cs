using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>The App can't be referenced, so its pages are read. A bare attribute value on a string parameter is a literal, not the page's property.</summary>
public partial class PageParameterTests
{
    [Test]
    public void NoPageAttribute_PassesAPropertyNameAsALiteral()
    {
        var offences = new List<string>();
        foreach (var file in RepoFiles.Find("*.razor", "src", "CalorieTracker.App", "Components", "Pages"))
        {
            var text = File.ReadAllText(file);
            var properties = PropertyName().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
            offences.AddRange(Attribute().Matches(text)
                .Where(m => properties.Contains(m.Groups[2].Value))
                .Select(m => $"{Path.GetFileName(file)}: {m.Value} (use @{m.Groups[2].Value})"));
        }

        Assert.That(offences, Is.Empty);
    }

    [Test]
    public void FoodsPage_PassesTheQueryStringValueToSearch()
    {
        var foods = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Foods.razor");

        Assert.That(foods, Does.Contain("InitialQuery=\"@Query\""));
    }

    [GeneratedRegex(@"public\s+string\??\s+(\w+)\s*\{\s*get;")]
    private static partial Regex PropertyName();

    [GeneratedRegex(@"\b([A-Z]\w*)=""(\w+)""")]
    private static partial Regex Attribute();
}
