using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.DesignSystem;

public partial class StatusMessageGutterTests
{
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
        var snackbar = Margin("Components/Feedback/Snackbar.razor.css", "snackbar");

        Assert.That(Margin("Screens/ServingView.razor.css", "status-error"), Is.EqualTo(snackbar));
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
