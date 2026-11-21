using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

public class TokensOnlyCssTests
{
    private static IEnumerable<TestCaseData> StyleFiles() =>
        RepoFiles.Find("*.css", "src", "CalorieTracker.UI")
            .Select(f => new TestCaseData(f).SetArgDisplayNames(Path.GetFileName(f)));

    private static IEnumerable<TestCaseData> MarkupFiles() =>
        RepoFiles.Find("*.razor", "src", "CalorieTracker.UI")
            .Select(f => new TestCaseData(f).SetArgDisplayNames(Path.GetFileName(f)));

    [TestCaseSource(nameof(StyleFiles))]
    public void Css_HasNoLiteralColoursOrSizes(string file) =>
        Assert.That(TokensOnlyCheck.Literals(File.ReadAllText(file), Path.GetFileName(file)), Is.Empty);

    [TestCaseSource(nameof(MarkupFiles))]
    public void InlineStyles_HaveNoLiteralColoursOrSizes(string file) =>
        Assert.That(TokensOnlyCheck.InlineStyleLiterals(File.ReadAllText(file)), Is.Empty);

    [TestCaseSource(nameof(StyleFiles))]
    public void Css_UsesOnlyDeclaredTokens(string file) =>
        Assert.That(TokensOnlyCheck.UnknownTokens(File.ReadAllText(file), KitNames.Tokens.ToList()), Is.Empty);

    [TestCaseSource(nameof(StyleFiles))]
    public void Css_FillsWithBackgroundColor_SoThePressedLayerShows(string file) =>
        Assert.That(TokensOnlyCheck.BackgroundShorthands(File.ReadAllText(file)), Is.Empty, "the kit's base.css pressed layer is a background-image, which the background shorthand resets");
}
