using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>A component or token the kit has goes to the kit (app-ui-kit), not into this app.</summary>
public class NoLocalKitCopiesTests
{
    private static readonly string UiFolder = RepoFiles.PathOf("src", "CalorieTracker.UI");

    /// <summary>Razor files named after a kit component, as paths relative to it.</summary>
    internal static IReadOnlyList<string> ComponentCopies(IEnumerable<string> razorFiles, IReadOnlySet<string> kitComponents, string folder) =>
        razorFiles
            .Where(file => file.EndsWith(".razor", StringComparison.Ordinal) && kitComponents.Contains(RazorName(file)))
            .Select(file => Path.GetRelativePath(folder, file))
            .ToList();

    /// <summary>Tokens a stylesheet declares that the kit also declares.</summary>
    internal static IReadOnlyList<string> TokenCopies(string css, IReadOnlySet<string> kitTokens) =>
        DesignTokens.Declares(css).Where(kitTokens.Contains).Distinct().ToList();

    [Test]
    public void AppComponents_AreNotCopiesOfKitComponents() =>
        Assert.That(ComponentCopies(RepoFiles.Find("*.razor", "src", "CalorieTracker.UI"), KitNames.Components, UiFolder), Is.Empty);

    [Test]
    public void AppTokens_AreNotCopiesOfKitTokens() =>
        Assert.That(TokenCopies(AppTokens.AppCss, KitNames.Tokens), Is.Empty);

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
    public void TokenCopies_FindsAKitTokenInATokensFile() =>
        Assert.That(TokenCopies(":root { --color-accent: #000000; --ring-size: 1px; }", KitNames.Tokens), Is.EqualTo(new[] { "--color-accent" }));

    [Test]
    public void KitNames_ListTheKitsComponentsAndTokens()
    {
        Assert.That(KitNames.Components, Does.Contain("Spinner"));
        Assert.That(KitNames.Tokens, Does.Contain("--color-accent"));
    }

    private static string RazorName(string file) => Path.GetFileNameWithoutExtension(file);
}
