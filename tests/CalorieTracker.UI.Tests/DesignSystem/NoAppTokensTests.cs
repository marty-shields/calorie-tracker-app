using System.Text.RegularExpressions;
using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>This app declares no CSS custom properties (tokens); a new one is requested in app-ui-kit and used from the kit.</summary>
public partial class NoAppTokensTests
{
    private const string MovesToKit = "moves to the kit: #164";

    /// <summary>Custom properties the app still declares until the kit releases them; the list only shrinks.</summary>
    private static readonly IReadOnlyDictionary<string, string> PendingKitTokens = new Dictionary<string, string>
    {
        ["--color-ring-track"] = MovesToKit,
        ["--color-protein"] = MovesToKit,
        ["--color-carbs"] = MovesToKit,
        ["--color-fat"] = MovesToKit,
        ["--color-target-band"] = MovesToKit,
        ["--font-size-xs"] = MovesToKit,
        ["--opacity-thin"] = MovesToKit,
        ["--ring-size"] = MovesToKit,
        ["--ring-width"] = MovesToKit,
        ["--ring-size-compact"] = MovesToKit,
        ["--ring-width-compact"] = MovesToKit,
        ["--share-bar-height"] = MovesToKit,
        ["--chart-dash"] = MovesToKit,
        ["--sheet-enter-duration"] = MovesToKit,
        ["--sheet-max-width"] = MovesToKit,
        ["--sheet-handle-width"] = MovesToKit,
        ["--sheet-handle-height"] = MovesToKit,
        ["--sheet-tile-min-height"] = MovesToKit,
        ["--sheet-tile-icon-size"] = MovesToKit,

        // Component-local properties of kit-bound components.
        ["--fill"] = MovesToKit,
        ["--size"] = MovesToKit,
        ["--stroke"] = MovesToKit,
        ["--icon-size"] = MovesToKit,
    };

    /// <summary>Names of the custom properties a stylesheet declares (<c>--name:</c>); <c>var(--name)</c> uses and comments do not count.</summary>
    internal static IReadOnlySet<string> Declared(string css) =>
        DesignTokens.Declares(CommentPattern().Replace(css, "")).ToHashSet(StringComparer.Ordinal);

    /// <summary>Names of the custom properties declared in the inline <c>style="..."</c> values of markup.</summary>
    internal static IReadOnlySet<string> DeclaredInline(string markup) =>
        StylePattern().Matches(markup)
            .SelectMany(match => DesignTokens.Declares(match.Groups["style"].Value + ";"))
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> DeclaredInApp() =>
        RepoFiles.Find("*.css", "src").SelectMany(file => Declared(File.ReadAllText(file))).ToHashSet(StringComparer.Ordinal)
            .Union(RepoFiles.Find("*.razor", "src").SelectMany(file => DeclaredInline(File.ReadAllText(file))))
            .ToHashSet(StringComparer.Ordinal);

    [Test]
    public void App_DeclaresOnlyPendingKitTokens() =>
        Assert.That(DeclaredInApp().Where(name => !PendingKitTokens.ContainsKey(name)), Is.Empty,
            "declare a token in app-ui-kit, not in this app");

    [Test]
    public void PendingKitTokens_AreAllStillDeclared()
    {
        var declared = DeclaredInApp();
        Assert.That(PendingKitTokens.Keys.Where(name => !declared.Contains(name)), Is.Empty,
            "remove the token from PendingKitTokens: the list only shrinks");
    }

    [Test]
    public void Declared_FindsDeclarationsButNotUsesOrComments() =>
        Assert.That(
            Declared("/* --old: 1 */ :root { --a: 1px; margin: var(--b); } .x { --c-d:2; width: calc(var(--e) * 2); }"),
            Is.EquivalentTo(new[] { "--a", "--c-d" }));

    [Test]
    public void DeclaredInline_FindsCustomPropertiesInStyleAttributes() =>
        Assert.That(
            DeclaredInline("<span style=\"--x: var(--y)\"></span><div style=\"--fill: @Fill; --gap-x: var(--space-2)\" class=\"a\"></div><p style=\"margin: var(--z)\"></p>"),
            Is.EquivalentTo(new[] { "--x", "--fill", "--gap-x" }));

    [GeneratedRegex("\\bstyle=\"(?<style>[^\"]*)\"")]
    private static partial Regex StylePattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();
}
