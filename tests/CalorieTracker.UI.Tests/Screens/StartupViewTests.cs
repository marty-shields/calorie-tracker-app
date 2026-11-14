using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class StartupViewTests : ComponentTestBase
{
    [Test]
    public void NotFailed_ShowsSpinner()
    {
        var cut = Context.Render<StartupView>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=startup-loading]").TextContent.Trim(), Is.EqualTo("Loading…"));
            Assert.That(cut.FindAll("[data-testid=startup-error]"), Is.Empty);
        });
    }

    [Test]
    public void Failed_ShowsErrorInsteadOfSpinner()
    {
        var cut = Context.Render<StartupView>(p => p.Add(x => x.Failed, true));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=startup-error]").TextContent, Is.EqualTo("Couldn't open the database"));
            Assert.That(cut.FindAll("[data-testid=startup-loading]"), Is.Empty);
        });
    }

    [Test]
    public void Failed_TryAgainRaisesOnRetry()
    {
        var retries = 0;
        var cut = Context.Render<StartupView>(p => p
            .Add(x => x.Failed, true)
            .Add(x => x.OnRetry, () => retries++));

        cut.Find("[data-testid=startup-retry]").Click();

        Assert.That(retries, Is.EqualTo(1));
    }

    [Test]
    public void NotFailed_OffersNoTryAgain()
    {
        var cut = Context.Render<StartupView>();

        Assert.That(cut.FindAll("[data-testid=startup-retry]"), Is.Empty);
    }
}
