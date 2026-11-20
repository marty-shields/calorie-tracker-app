namespace CalorieTracker.UI.Tests.DesignSystem;

public class AppTokensTests
{
    [Test]
    public void AppTokens_DeclareNoTokenTheKitDeclares()
    {
        var kit = DesignTokens.Declares(DesignTokens.KitTokensCss).ToHashSet();

        var repeated = DesignTokens.Declares(DesignTokens.AppCss).Where(kit.Contains);

        Assert.That(repeated, Is.Empty);
    }
}
