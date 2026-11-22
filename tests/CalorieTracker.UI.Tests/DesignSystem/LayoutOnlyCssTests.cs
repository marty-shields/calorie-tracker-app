using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>App stylesheets and inline styles place things and size them; how they look (colour, border, font, shadow) comes from app-ui-kit.</summary>
public partial class LayoutOnlyCssTests
{
    private static readonly string[] AppFolders = ["src/CalorieTracker.UI", "src/CalorieTracker.App"];

    /// <summary>Properties that may be declared. Custom properties (<c>--x</c>) are left to <see cref="NoAppTokensTests"/>.</summary>
    [GeneratedRegex(
        "^(display|gap|row-gap|column-gap|order|width|min-width|max-width|height|min-height|max-height|inline-size|block-size|box-sizing|position|top|right|bottom|left|z-index|aspect-ratio|white-space|text-align|visibility|content|pointer-events|grid(-.*)?|flex(-.*)?|place-.*|align-.*|justify-.*|margin(-.*)?|padding(-.*)?|inset(-.*)?|overflow(-.*)?|container-.*|list-style|list-style-type)$")]
    private static partial Regex LayoutProperty();

    /// <summary>The <c>selector { property }</c> pairs of a stylesheet whose property is not a layout property, as "selector { property }".</summary>
    internal static IReadOnlyList<string> Violations(string css) =>
        RulePattern().Matches(CommentPattern().Replace(css, ""))
            .SelectMany(rule => NonLayout(rule.Groups["body"].Value).Select(property => $"{Squash(rule.Groups["selector"].Value)} {{ {property} }}"))
            .ToList();

    /// <summary>The non-layout properties declared in the inline <c>style="..."</c> or <c>style='...'</c> or unquoted <c>style=...</c> values of markup, and "@" for a style value computed in Razor, whose declarations cannot be checked. A <c>&lt;style</c> block counts as its own violation.</summary>
    internal static IReadOnlyList<string> InlineViolations(string markup) =>
        StylePattern().Matches(markup)
            .Select(match => match.Groups["style"].Success ? match.Groups["style"].Value : match.Groups["single"].Success ? match.Groups["single"].Value : match.Groups["bare"].Value)
            .SelectMany(value => value.Contains('@', StringComparison.Ordinal) ? ["@"] : NonLayout(value))
            .Concat(StyleBlockPattern().Matches(markup).Select(_ => "<style>"))
            .ToList();

    private static IEnumerable<string> NonLayout(string declarations) =>
        declarations.Split(';')
            .Select(declaration => declaration.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => parts[0].Trim().ToLowerInvariant())
            .Where(property => property.Length > 0 && !property.StartsWith("--", StringComparison.Ordinal) && !LayoutProperty().IsMatch(property));

    private static string Squash(string selector) => WhitespacePattern().Replace(selector.Trim(), " ");

    private static string Relative(string file) => Path.GetRelativePath(RepoFiles.Root, file);

    [Test]
    public void AppCss_DeclaresOnlyLayoutProperties()
    {
        var found = AppFolders
            .SelectMany(folder => RepoFiles.Find("*.css", folder.Split('/')))
            .SelectMany(file => Violations(File.ReadAllText(file)).Select(violation => $"{Relative(file)}: {violation}"))
            .ToList();
        Assert.That(found, Is.Empty, "a look comes from app-ui-kit, not from app CSS; request the part there");
    }

    [Test]
    public void AppMarkup_HasNoVisualInlineStyle()
    {
        var found = AppFolders
            .SelectMany(folder => RepoFiles.Find("*.razor", folder.Split('/')).Concat(RepoFiles.Find("*.html", folder.Split('/'))))
            .SelectMany(file => InlineViolations(File.ReadAllText(file)).Select(property => $"{Relative(file)}: style \"{property}\""))
            .ToList();
        Assert.That(found, Is.Empty, "a look comes from app-ui-kit, not from an inline style; request the part there");
    }

    [Test]
    public void Violations_NamesTheSelectorAndPropertyOfAVisualDeclaration() =>
        Assert.That(
            Violations("/* color: red; */ .a { display: flex; gap: var(--space-2); }\n.screen   .title {\n  margin: 0;\n  color: red;\n  FONT-WEIGHT: bold;\n}"),
            Is.EqualTo(new[] { ".screen .title { color }", ".screen .title { font-weight }" }));

    [Test]
    public void Violations_AcceptsEveryLayoutProperty() =>
        Assert.That(
            Violations(".a { display: grid; grid-template-columns: 1fr; flex: 1 1 0; flex-wrap: wrap; gap: 1px; place-items: center; align-self: stretch; justify-content: center; order: 1; margin: 0; padding-inline: 1px; width: 1px; min-height: 1px; box-sizing: border-box; position: absolute; inset: 0; z-index: 1; overflow-wrap: anywhere; container-type: inline-size; aspect-ratio: 1; white-space: nowrap; text-align: center; visibility: hidden; content: ''; pointer-events: none; list-style: none; --x: 1; }"),
            Is.Empty);

    [Test]
    public void InlineViolations_FindsAVisualInlineStyle() =>
        Assert.That(
            InlineViolations("<p style=\"font-weight: bold\"></p><div style=\"--fill: 1; margin: var(--space-2)\"></div><span style=\"color: red; gap: 1px\"></span>"),
            Is.EqualTo(new[] { "font-weight", "color" }));

    [Test]
    public void InlineViolations_FindsSingleQuotedComputedAndBlockStyles()
    {
        Assert.Multiple(() =>
        {
            Assert.That(InlineViolations("<p style='color: red; gap: 1px'></p>"), Is.EqualTo(new[] { "color" }));
            Assert.That(InlineViolations("<p style=\"@($\"color: {x}\")\"></p>"), Is.EqualTo(new[] { "@" }));
            Assert.That(InlineViolations("<p style=\"@Style\"></p>"), Is.EqualTo(new[] { "@" }));
            Assert.That(InlineViolations("<p style=color:red></p>"), Is.EqualTo(new[] { "color" }));
            Assert.That(InlineViolations("<p style=@x></p>"), Is.EqualTo(new[] { "@" }));
            Assert.That(InlineViolations("<p style=\"a @x\"></p>"), Is.EqualTo(new[] { "@" }));
            Assert.That(InlineViolations("<STYLE>.x{color:red}</STYLE>"), Is.EqualTo(new[] { "<style>" }));
            Assert.That(InlineViolations("<p STYLE=\"color:red\"></p>"), Is.EqualTo(new[] { "color" }));
            Assert.That(InlineViolations("<p style = \"color:red\"></p>"), Is.EqualTo(new[] { "color" }));
            Assert.That(InlineViolations("<p data-style=compact></p><p data-style=\"color:red\"></p>"), Is.Empty);
        });
    }

    [Test]
    public void Violations_RejectsListStyleImage() =>
        Assert.That(Violations(".a { list-style-image: url(x.png); list-style-type: none; }"), Is.EqualTo(new[] { ".a { list-style-image }" }));

    [GeneratedRegex("(?<selector>[^{}]+)\\{(?<body>[^{}]*)\\}")]
    private static partial Regex RulePattern();

    [GeneratedRegex("(?<=\\s)style\\s*=\\s*(?:\"(?<style>[^\"]*)\"|'(?<single>[^']*)'|(?<bare>[^\\s>\"']+))", RegexOptions.IgnoreCase)]
    private static partial Regex StylePattern();

    [GeneratedRegex("<style\\b", RegexOptions.IgnoreCase)]
    private static partial Regex StyleBlockPattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
