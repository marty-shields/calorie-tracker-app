using System.Text.RegularExpressions;
using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

public partial class StatusMessageGutterTests
{
    private const string GapToken = "--snackbar-gap-bottom";
    private const string InlineToken = "--snackbar-gap-inline";

    private static string Body(string file, string className)
    {
        var css = CommentRegex().Replace(RepoFiles.Read("src", "CalorieTracker.UI", file), "");
        return RepoFiles.Rule(css, className);
    }

    private static string Margin(string file, string className)
    {
        var body = Body(file, className);
        var margins = MarginRegex().Matches(body);
        Assert.That(margins, Has.Count.EqualTo(1), $".{className} in {file} needs exactly one margin declaration");
        Assert.That(body, Does.Not.Match(@"margin-(left|right|inline|block)"), $".{className} in {file} uses a margin longhand");
        return margins[0].Groups["value"].Value.Trim();
    }

    [Test]
    public void ServingStatusError_HasTheSnackbarMargins()
    {
        Assert.That(KitCss.Tokens, Does.Contain(GapToken), "the kit's Snackbar gap token");
        Assert.That(KitCss.Tokens, Does.Contain(InlineToken), "the kit's Snackbar inline gap token");

        Assert.That(Margin("Screens/ServingView.razor.css", "status-error"), Is.EqualTo($"0 var({InlineToken}) var({GapToken})"), "the margins are the kit's Snackbar gap tokens, not literal copies of their values");
    }

    [GeneratedRegex(@"(?:^|[;{\s])margin:\s*(?<value>[^;]+);")]
    private static partial Regex MarginRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();
}
