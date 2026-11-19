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

    [Test]
    public void ServingPage_TakesTheCarriedAmountAndServingsOnlyWithoutAnEntryId()
    {
        var serving = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Serving.razor");

        Assert.Multiple(() =>
        {
            Assert.That(serving, Does.Contain("StartAmount=\"EntryId is null ? ServingRoute.ParseNumber(AmountText) : null\""));
            Assert.That(serving, Does.Contain("StartServings=\"EntryId is null ? ServingRoute.ParseNumber(ServingsText) : null\""));
            Assert.That(serving, Does.Contain("StartServingId=\"EntryId is null ? ServingRoute.ParseId(ServingText) : null\""));
            Assert.That(serving, Does.Contain("EntryServingId=\"ServingRoute.ParseId(ServingText)\""));
        });
    }

    [Test]
    public void FoodsPage_CarriesTheLastAmountAndServingsToTheServingRoute()
    {
        var foods = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Foods.razor");

        Assert.That(Regex.Matches(foods, @"amount: request\.LastAmount, servings: request\.LastServings, servingId: request\.LastServingId"), Has.Count.EqualTo(1));
    }

    [GeneratedRegex(@"public\s+string\??\s+(\w+)\s*\{")]
    private static partial Regex PropertyName();

    [GeneratedRegex(@"(?<![\w@])([A-Z]\w*)=""(\w+)""")]
    private static partial Regex Attribute();
}
