using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class SettingsViewTests : ComponentTestBase
{
    [Test]
    public void Title_IsSettings()
    {
        var cut = Render();

        Assert.That(cut.Find("[data-testid=screen-title]").TextContent, Is.EqualTo("Settings"));
    }

    [Test]
    public void Credits_NameBothDatasetsAndTheirLicences()
    {
        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=credit-cofid]").TextContent, Does.Contain("CoFID").And.Contain("Open Government Licence v3.0"));
            Assert.That(cut.Find("[data-testid=credit-openfoodfacts]").TextContent, Does.Contain("Open Food Facts").And.Contain("Open Database Licence"));
        });
    }

    [Test]
    public void Version_WhenSet_ShowsIt()
    {
        var cut = Context.Render<SettingsView>(p => p.Add(x => x.Version, "1.4.2").Add(x => x.OnBack, () => { }));

        Assert.That(cut.Find("[data-testid=app-version]").TextContent, Is.EqualTo("Version 1.4.2"));
    }

    [Test]
    public void Version_WhenNull_ShowsNothing()
    {
        var cut = Render();

        Assert.That(cut.FindAll("[data-testid=app-version]"), Is.Empty);
    }

    [Test]
    public void Theme_ShowsTheChoiceSelected()
    {
        var cut = Context.Render<SettingsView>(p => p.Add(x => x.Theme, ThemePreference.Dark).Add(x => x.OnBack, () => { }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=settings-theme-dark]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=settings-theme-system]").HasAttribute("checked"), Is.False);
            Assert.That(cut.Find("[data-testid=settings-theme-light]").HasAttribute("checked"), Is.False);
        });
    }

    [Test]
    public void Theme_Tapped_RaisesOnThemeChanged()
    {
        ThemePreference? chosen = null;
        var cut = Context.Render<SettingsView>(p => p
            .Add(x => x.OnThemeChanged, t => chosen = t)
            .Add(x => x.OnBack, () => { }));

        cut.Find("[data-testid=settings-theme-dark]").Change(true);

        Assert.That(chosen, Is.EqualTo(ThemePreference.Dark));
    }

    [Test]
    public void Back_RaisesOnBack()
    {
        var back = false;
        var cut = Context.Render<SettingsView>(p => p.Add(x => x.OnBack, () => back = true));

        cut.Find("[data-testid=back]").Click();

        Assert.That(back, Is.True);
    }

    private IRenderedComponent<SettingsView> Render() =>
        Context.Render<SettingsView>(p => p.Add(x => x.OnBack, () => { }));
}
