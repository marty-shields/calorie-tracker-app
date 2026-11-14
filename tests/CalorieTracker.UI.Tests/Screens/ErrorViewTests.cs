using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class ErrorViewTests : ComponentTestBase
{
    [Test]
    public void ShowsTheMessage() =>
        Assert.That(Context.Render<ErrorView>().Find("[data-testid=error-message]").TextContent, Is.EqualTo("Something went wrong"));

    [Test]
    public void Restart_RaisesOnRestart()
    {
        var restarted = 0;
        var cut = Context.Render<ErrorView>(p => p.Add(x => x.OnRestart, () => restarted++));

        cut.Find("[data-testid=error-restart]").Click();

        Assert.That(restarted, Is.EqualTo(1));
    }
}
