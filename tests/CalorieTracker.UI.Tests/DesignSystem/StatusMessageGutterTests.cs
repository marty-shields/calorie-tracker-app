using System.Text.RegularExpressions;
using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

public partial class StatusMessageGutterTests
{
    private const string GapToken = "--snackbar-gap-bottom";

    private static string Body(string file, string className)
    {
        var css = CommentRegex().Replace(RepoFiles.Read("src", "CalorieTracker.UI", file), "");
        return RepoFiles.Rule(css, className);
    }

    private static string Margin(string file, string className)
    {
        var body = Body(file, className);
        return MarginOf(body, file, className);
    }

    private static string MarginOf(string body, string file, string className)
    {
        var margins = MarginRegex().Matches(body);
        Assert.That(margins, Has.Count.EqualTo(1), $".{className} in {file} needs exactly one margin declaration");
        Assert.That(body, Does.Not.Match(@"margin-(left|right|inline|block)"), $".{className} in {file} uses a margin longhand");
        return margins[0].Groups["value"].Value.Trim();
    }

    [Test]
    public void ServingStatusError_HasTheSnackbarMargins()
    {
        Assert.That(KitCss.Tokens, Does.Contain("--snackbar-gap-bottom"), "the kit's Snackbar gap token");

        var css = CommentRegex().Replace(RepoFiles.Read("src", "CalorieTracker.UI", "Screens/ServingView.razor.css"), "");
        var rule = Regex.Match(css, @"\.error\.status-error\s*\{(?<body>[^}]*)\}");
        Assert.That(rule.Success, Is.True, "the status margins need the .error.status-error selector so they beat .error whatever the order");

        Assert.That(MarginOf(rule.Groups["body"].Value, "Screens/ServingView.razor.css", "error.status-error"), Is.EqualTo($"0 var(--space-4) var({GapToken})"), "the bottom margin is the kit's Snackbar gap token, not a literal copy of its value");
    }

    [Test]
    public void ServingInFormError_KeepsNoMargin()
    {
        Assert.That(Margin("Screens/ServingView.razor.css", "error"), Is.EqualTo("0"));
    }

    [GeneratedRegex(@"(?:^|[;{\s])margin:\s*(?<value>[^;]+);")]
    private static partial Regex MarginRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();
}
