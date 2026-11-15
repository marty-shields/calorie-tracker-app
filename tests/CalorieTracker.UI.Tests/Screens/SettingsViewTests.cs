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
    public void Target_ShowsTheNumberUnitAndHelper()
    {
        var cut = Context.Render<SettingsView>(p => p.Add(x => x.Target, 2100m).Add(x => x.OnBack, () => { }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=settings-target]").GetAttribute("value"), Is.EqualTo("2100"));
            Assert.That(cut.Find("[data-testid=settings-target-unit]").TextContent, Is.EqualTo("kcal"));
            Assert.That(cut.Find("[data-testid=settings-target-hint]").TextContent, Is.EqualTo("Shown on the dashboard. Leave it empty for no target."));
            Assert.That(cut.FindAll("[data-testid=settings-target-error]"), Is.Empty);
        });
    }

    [Test]
    public void Target_WhenNull_ShowsAnEmptyField()
    {
        var cut = Render();

        Assert.That(cut.Find("[data-testid=settings-target]").GetAttribute("value"), Is.EqualTo(""));
    }

    [Test]
    public void Target_Invalid_ShowsTheMessageInPlaceOfTheHelper()
    {
        var cut = Context.Render<SettingsView>(p => p
            .Add(x => x.Target, 0m)
            .Add(x => x.TargetInvalidMessage, "Enter a whole number above 0, or clear the field.")
            .Add(x => x.OnBack, () => { }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=settings-target-error]").TextContent, Is.EqualTo("Enter a whole number above 0, or clear the field."));
            Assert.That(cut.FindAll("[data-testid=settings-target-hint]"), Is.Empty);
        });
    }

    [Test]
    public void Target_Typed_RaisesOnTargetChanged()
    {
        var typed = new List<decimal?>();
        var cut = Context.Render<SettingsView>(p => p
            .Add(x => x.OnTargetChanged, v => typed.Add(v))
            .Add(x => x.OnBack, () => { }));

        cut.Find("[data-testid=settings-target]").Input("1800");
        cut.Find("[data-testid=settings-target]").Input("");

        Assert.That(typed, Is.EqualTo(new decimal?[] { 1800m, null }));
    }

    [Test]
    public async Task Back_RaisesOnBack()
    {
        var back = false;
        var cut = Context.Render<SettingsView>(p => p.Add(x => x.OnBack, () => back = true));

        await cut.Find("[data-testid=back]").ClickAsync(new());

        Assert.That(back, Is.True);
    }

    private IRenderedComponent<SettingsView> Render() =>
        Context.Render<SettingsView>(p => p.Add(x => x.OnBack, () => { }));
}
