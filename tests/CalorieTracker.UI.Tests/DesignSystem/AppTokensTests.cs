namespace CalorieTracker.UI.Tests.DesignSystem;

public class AppTokensTests
{
    // Tokens the app still sets to its own value, each removed by the issue named in tokens.css.
    private static readonly string[] TemporaryOverrides = ["--add-button-lift"];

    [Test]
    public void AppTokens_DeclareNoTokenTheKitDeclares_ExceptTheTemporaryOverrides()
    {
        var kit = DesignTokens.Declares(DesignTokens.KitTokensCss).ToHashSet();

        var repeated = DesignTokens.Declares(DesignTokens.AppCss).Where(kit.Contains).Except(TemporaryOverrides);

        Assert.That(repeated, Is.Empty);
    }

    [Test]
    public void TemporaryOverrides_DifferFromTheKit_SoTheyAreNotJustCopies() =>
        Assert.Multiple(() =>
        {
            foreach (var name in TemporaryOverrides)
            {
                Assert.That(DesignTokens.Declares(DesignTokens.AppCss), Has.Member(name), name);
                Assert.That(Value(DesignTokens.AppCss, name), Is.Not.EqualTo(Value(DesignTokens.KitTokensCss, name)), name);
            }
        });

    private static string Value(string css, string name) =>
        System.Text.RegularExpressions.Regex.Match(css, @"^\s*" + name + @"\s*:([^;]*);", System.Text.RegularExpressions.RegexOptions.Multiline).Groups[1].Value.Trim();
}
