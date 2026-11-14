using CalorieTracker.Core.Services;
using CalorieTracker.UI.Components.Inputs;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace CalorieTracker.UI.Tests.Screens;

public class SettingsPageTests : ComponentTestBase
{
    private FakeLogger<SettingsPage> logger = null!;

    [SetUp]
    public void SetUp()
    {
        Context.Services.AddSingleton<IAppEnvironment>(new FakeEnvironment());
        logger = new FakeLogger<SettingsPage>();
        Context.Services.AddSingleton<ILogger<SettingsPage>>(logger);
        Context.JSInterop.Setup<string>("calorieTrackerTheme.get", _ => true).SetResult("system");
        Context.JSInterop.Setup<bool>("calorieTrackerTheme.set", _ => true).SetResult(true);
    }

    [Test]
    public void Version_ShowsTheInstalledVersion()
    {
        var cut = Context.Render<SettingsPage>();

        Assert.That(cut.Find("[data-testid=app-version]").TextContent, Is.EqualTo("Version 2.3.4"));
    }

    [Test]
    public async Task Theme_DefaultsToSystem()
    {
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        Assert.That(cut.Find("[data-testid=settings-theme-system]").HasAttribute("checked"), Is.True);
    }

    [Test]
    public async Task Theme_ShowsTheSavedChoice()
    {
        Context.JSInterop.Setup<string>("calorieTrackerTheme.get", _ => true).SetResult("dark");
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=settings-theme-dark]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=settings-theme-system]").HasAttribute("checked"), Is.False);
        });
    }

    [Test]
    public async Task Theme_Tapped_SavesAndShowsTheChoice()
    {
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        cut.Find("[data-testid=settings-theme-light]").Change(true);

        Assert.Multiple(() =>
        {
            Assert.That(Context.JSInterop.Invocations["calorieTrackerTheme.set"].Single().Arguments, Is.EqualTo(new object[] { "light" }));
            Assert.That(cut.Find("[data-testid=settings-theme-light]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public async Task Theme_ReadFails_ShowsSystemAndLogsTheWarning()
    {
        var exception = new JSException("no storage");
        Context.JSInterop.Setup<string>("calorieTrackerTheme.get", _ => true).SetException(exception);
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=settings-theme-system]").HasAttribute("checked"), Is.True);
            Assert.That(cut.FindAll("[data-testid=settings-loading]"), Is.Empty);
            Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[] { (UiEvent.ThemeLoadFailed, LogLevel.Warning, "Reading the saved theme failed") }));
            Assert.That(logger.LatestRecord.Exception, Is.SameAs(exception));
        });
    }

    [Test]
    public async Task Theme_SaveFails_KeepsThePreviousChoiceAndLogsTheWarning()
    {
        var exception = new JSException("no storage");
        Context.JSInterop.Setup<bool>("calorieTrackerTheme.set", _ => true).SetException(exception);
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        var control = cut.FindComponent<SegmentedControl<ThemePreference>>();
        var renders = control.RenderCount;
        cut.Find("[data-testid=settings-theme-light]").Change(true);

        cut.WaitForAssertion(() => Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[] { (UiEvent.ThemeSaveFailed, LogLevel.Warning, "Saving the theme failed") })));
        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=settings-theme-system]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=settings-theme-light]").HasAttribute("checked"), Is.False);
            Assert.That(control.RenderCount - renders, Is.EqualTo(2));
            Assert.That(logger.LatestRecord.Exception, Is.SameAs(exception));
        });
    }

    [Test]
    public async Task Theme_StorageRefusesTheSave_KeepsTheChoiceAndLogsTheWarning()
    {
        Context.JSInterop.Setup<bool>("calorieTrackerTheme.set", _ => true).SetResult(false);
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        cut.Find("[data-testid=settings-theme-light]").Change(true);

        cut.WaitForAssertion(() => Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[] { (UiEvent.ThemeNotSaved, LogLevel.Warning, "Storage refused the saved theme; it applies until the app closes") })));
        Assert.That(cut.Find("[data-testid=settings-theme-light]").HasAttribute("checked"), Is.True);
    }

    [Test]
    public void Back_RaisesOnBack()
    {
        var backs = 0;
        var cut = Context.Render<SettingsPage>(p => p.Add(x => x.OnBack, () => backs++));

        cut.Find("[data-testid=back]").Click();

        Assert.That(backs, Is.EqualTo(1));
    }

    [Test]
    public async Task AndroidBack_RaisesOnBack()
    {
        var backs = 0;
        var cut = Context.Render<SettingsPage>(p => p.Add(x => x.OnBack, () => backs++));

        await cut.InvokeAsync(BackButton.HandleAsync);

        Assert.That(backs, Is.EqualTo(1));
    }

    private sealed class FakeEnvironment : IAppEnvironment
    {
        public string AppVersion => "2.3.4";
    }
}
