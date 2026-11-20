using CalorieTracker.UI.Stories.Stories.Foundations;

namespace CalorieTracker.UI.Tests.DesignSystem;

public class AppTokenNamesTests
{
    [Test]
    public void TokensStory_ListsEveryTokenInTheAppsTokensCss() =>
        Assert.That(AppTokenNames.Colours.Concat(AppTokenNames.Sizes), Is.EquivalentTo(AppTokens.AppNames));

    [Test]
    public void Colours_AreTheColourTokens() =>
        Assert.That(AppTokenNames.Colours, Has.All.StartWith("--color-"));

    [Test]
    public void Sizes_AreNotColourTokens() =>
        Assert.That(AppTokenNames.Sizes, Has.None.StartWith("--color-"));
}
