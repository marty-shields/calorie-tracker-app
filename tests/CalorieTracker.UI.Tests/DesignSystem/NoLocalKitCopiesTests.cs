using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>A component or token the kit has goes to the kit (app-ui-kit), not into this app.</summary>
public class NoLocalKitCopiesTests
{
    private static readonly string UiFolder = RepoFiles.PathOf("src", "CalorieTracker.UI");

    private static readonly IReadOnlySet<string> KitTypeNames = KitNames.Types;

    /// <summary>
    /// Source and razor files named after a kit component or public kit type, as paths relative to the folder.
    /// The name is what comes before the first dot, so <c>Spinner.razor.css</c> and <c>Spinner.razor.cs</c> count.
    /// </summary>
    internal static IReadOnlyList<string> ComponentCopies(IEnumerable<string> files, IReadOnlySet<string> kitNames, string folder) =>
        files
            .Where(file => (file.EndsWith(".razor", StringComparison.Ordinal) || file.EndsWith(".cs", StringComparison.Ordinal))
                && kitNames.Contains(KitName(file)))
            .Select(file => Path.GetRelativePath(folder, file))
            .ToList();

    /// <summary>Tokens a stylesheet declares that the kit also declares.</summary>
    internal static IReadOnlyList<string> TokenCopies(string css, IReadOnlySet<string> kitTokens) =>
        DesignTokens.Declares(css).Where(kitTokens.Contains).Distinct().ToList();

    [Test]
    public void AppFiles_AreNotCopiesOfKitComponentsOrTypes() =>
        Assert.That(
            ComponentCopies(RepoFiles.Find("*.razor", "src", "CalorieTracker.UI").Concat(RepoFiles.Find("*.cs", "src", "CalorieTracker.UI")), KitNames.Components.Union(KitTypeNames).ToHashSet(), UiFolder),
            Is.Empty);

    [Test]
    public void AppCss_DoesNotDeclareKitTokens() =>
        Assert.That(RepoFiles.Find("*.css", "src").SelectMany(file => TokenCopies(File.ReadAllText(file), KitNames.Tokens)), Is.Empty);

    [Test]
    public void ComponentCopies_FindsAFileNamedAfterAKitComponent()
    {
        var copies = ComponentCopies(
            [Path.Combine(UiFolder, "Components", "Spinner.razor"), Path.Combine(UiFolder, "Components", "Spinner.razor.css"), Path.Combine(UiFolder, "Screens", "ScanView.razor")],
            KitNames.Components,
            UiFolder);

        Assert.That(copies, Is.EqualTo(new[] { Path.Combine("Components", "Spinner.razor") }));
    }

    [Test]
    public void ComponentCopies_FindsASourceFileNamedAfterAKitType()
    {
        var copies = ComponentCopies(
            [Path.Combine(UiFolder, "SnackbarService.cs"), Path.Combine(UiFolder, "Components", "Spinner.razor.cs"), Path.Combine(UiFolder, "Screens", "ScanView.cs")],
            KitNames.Components.Union(KitTypeNames).ToHashSet(),
            UiFolder);

        Assert.That(copies, Is.EqualTo(new[] { "SnackbarService.cs", Path.Combine("Components", "Spinner.razor.cs") }));
    }

    [Test]
    public void TokenCopies_FindsAKitTokenInAStylesheet() =>
        Assert.That(TokenCopies(":root { --color-accent: #000000; --local-size: 1px; }", KitNames.Tokens), Is.EqualTo(new[] { "--color-accent" }));

    [Test]
    public void KitNames_ListTheKitsComponentsAndTokens()
    {
        Assert.That(KitNames.Components, Does.Contain("Spinner"));
        Assert.That(KitNames.Tokens, Does.Contain("--color-accent"));
    }

    private static string KitName(string file) => Path.GetFileName(file).Split('.')[0];
}
