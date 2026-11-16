using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.Conventions;

public partial class FoodTypeUseTests
{
    /// <summary>Where <c>FoodType</c> may appear: the stored column and its upgrade, the seed, and the Open Food Facts Drink rule that sets it.</summary>
    private static readonly string[] Allowed =
    [
        "src/CalorieTracker.Core/Data/",
        "src/CalorieTracker.Core/Seed/",
        "src/CalorieTracker.Core/Foods/OpenFoodFactsLookup.cs",
    ];

    [GeneratedRegex(@"\bFoodType\b|\b[Ff]ood\w*\s*\??\.\s*Type\b")]
    private static partial Regex TypeUse();

    [Test]
    public void NoCodeOutsideCoreDataAndSeed_ReadsTheFoodType()
    {
        var offenders = new[] { "*.cs", "*.razor" }
            .SelectMany(pattern => RepoFiles.Find(pattern, "src"))
            .Select(file => Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/'))
            .Where(file => !Allowed.Any(file.StartsWith))
            .Where(file => TypeUse().IsMatch(File.ReadAllText(RepoFiles.PathOf(file))))
            .ToList();

        Assert.That(offenders, Is.Empty);
    }
}
