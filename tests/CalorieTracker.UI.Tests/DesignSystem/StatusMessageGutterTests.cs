using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.DesignSystem;

public class StatusMessageGutterTests
{
    private static string Margin(string file, string selector)
    {
        var css = RepoFiles.Read("src", "CalorieTracker.UI", file);
        var rule = Regex.Match(css, @"(?:^|\})\s*" + Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        Assert.That(rule.Success, $"{selector} not found in {file}");
        var margin = Regex.Match(rule.Groups["body"].Value, @"margin:\s*(?<value>[^;]+);");
        Assert.That(margin.Success, $"{selector} has no margin in {file}");
        return margin.Groups["value"].Value.Trim();
    }

    [Test]
    public void ServingError_HasTheSnackbarMargins()
    {
        var snackbar = Margin("Components/Feedback/Snackbar.razor.css", ".snackbar");

        Assert.That(Margin("Screens/ServingView.razor.css", ".error"), Is.EqualTo(snackbar));
    }
}
