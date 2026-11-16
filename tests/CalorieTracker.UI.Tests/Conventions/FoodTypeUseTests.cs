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

    /// <summary>The Android <c>WindowInsets.Type</c> calls, which have nothing to do with a food.</summary>
    private static readonly string[] WindowInsetsFiles =
    [
        "src/CalorieTracker.App/Keyboard/AndroidKeyboardRestore.cs",
        "src/CalorieTracker.App/Components/SystemBarInsets.cs",
    ];

    [GeneratedRegex(@"\bFoodType\b|\.\s*Type\b")]
    private static partial Regex TypeUse();

    [GeneratedRegex(@"//[^\r\n]*|/\*.*?\*/|@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex Comments();

    private static string WithoutComments(string source) => Comments().Replace(source, "");

    [Test]
    public void NoCodeOutsideCoreDataAndSeed_ReadsTheFoodType()
    {
        var scanned = new[] { "*.cs", "*.razor" }
            .SelectMany(pattern => RepoFiles.Find(pattern, "src"))
            .Select(file => (Path: Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/'), Full: file))
            .Where(file => !Allowed.Any(file.Path.StartsWith))
            .ToList();

        Assert.That(scanned, Is.Not.Empty);

        var offenders = scanned
            .Where(file => !WindowInsetsFiles.Contains(file.Path))
            .Where(file => TypeUse().IsMatch(WithoutComments(File.ReadAllText(file.Full))))
            .Select(file => file.Path)
            .ToList();

        Assert.That(offenders, Is.Empty);
    }
}
