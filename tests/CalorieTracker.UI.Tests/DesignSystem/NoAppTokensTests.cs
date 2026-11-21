using System.Text.RegularExpressions;

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
        DeclarationPattern().Matches(CommentPattern().Replace(css, ""))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> DeclaredInApp() =>
        RepoFiles.Find("*.css", "src").SelectMany(file => Declared(File.ReadAllText(file))).ToHashSet(StringComparer.Ordinal);

    [Test]
    public void AppCss_DeclaresOnlyPendingKitTokens() =>
        Assert.That(DeclaredInApp().Where(name => !PendingKitTokens.ContainsKey(name)), Is.Empty,
            "declare a token in app-ui-kit, not in this app");

    [Test]
    public void PendingKitTokens_AreAllStillDeclared() =>
        Assert.That(PendingKitTokens.Keys.Where(name => !DeclaredInApp().Contains(name)), Is.Empty,
            "remove the token from PendingKitTokens: the list only shrinks");

    [Test]
    public void Declared_FindsDeclarationsButNotUsesOrComments() =>
        Assert.That(
            Declared("/* --old: 1 */ :root { --a: 1px; margin: var(--b); } .x { --c-d:2; width: calc(var(--e) * 2); }"),
            Is.EquivalentTo(new[] { "--a", "--c-d" }));

    [GeneratedRegex(@"(?<![\w-])(?<name>--[\w-]+)\s*:")]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();
}
