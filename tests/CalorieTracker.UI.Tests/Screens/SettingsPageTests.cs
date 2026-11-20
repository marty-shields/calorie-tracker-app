using CalorieTracker.Core;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Services;
using CalorieTracker.Core.Targets;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;
using Shields.UI.Components.Inputs;
using Shields.UI.Theme;

namespace CalorieTracker.UI.Tests.Screens;

public class SettingsPageTests : ComponentTestBase
{
    private FakeLogger<SettingsPage> logger = null!;

    private const string InvalidMessage = "Enter a whole number above 0, or clear the field.";

    private string databasePath = null!;
    private ServiceProvider core = null!;
    private TargetService targets = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"settings-page-{Guid.NewGuid():N}.db3");
        core = new ServiceCollection().AddLogging().AddCore(databasePath).BuildServiceProvider();
        await core.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        targets = core.GetRequiredService<TargetService>();
        Context.Services.AddSingleton(targets);
        Context.Services.AddSingleton<IAppEnvironment>(new FakeEnvironment());
        logger = new FakeLogger<SettingsPage>();
        Context.Services.AddSingleton<ILogger<SettingsPage>>(logger);
        Context.JSInterop.Setup<string>("shieldsTheme.get", _ => true).SetResult("system");
        Context.JSInterop.Setup<bool>("shieldsTheme.set", _ => true).SetResult(true);
    }

    [TearDown]
    public async Task TearDown()
    {
        await core.DisposeAsync();
        File.Delete(databasePath);
    }

    private string TargetValue(IRenderedComponent<SettingsPage> cut) => cut.Find("[data-testid=settings-target]").GetAttribute("value")!;

    /// <summary>Types into the field and waits for the page's save to finish: the database runs calls in order, so a read made now ends after it.</summary>
    private async Task TypeAsync(IRenderedComponent<SettingsPage> cut, string text, TargetService? service = null)
    {
        await cut.Find("[data-testid=settings-target]").InputAsync(text);
        await (service ?? targets).GetAsync();
        await WaitForRendererAsync();
    }

    /// <summary>Renders the page and waits until its first target read has finished: the database runs calls in order, so a call made now ends after it.</summary>
    private async Task<IRenderedComponent<SettingsPage>> RenderLoadedAsync(TargetService service)
    {
        var cut = Context.Render<SettingsPage>();
        await service.GetAsync();
        await WaitForRendererAsync();
        return cut;
    }

    [Test]
    public async Task Target_NoneSaved_ShowsAnEmptyField()
    {
        var cut = await RenderLoadedAsync(targets);

        Assert.That(TargetValue(cut), Is.EqualTo(""));
    }

    [Test]
    public async Task Target_Saved_ShowsIt()
    {
        await targets.SaveAsync(2100);
        var cut = await RenderLoadedAsync(targets);
        Assert.That(TargetValue(cut), Is.EqualTo("2100"));
    }

    [Test]
    public async Task Target_Typed_IsSaved()
    {
        var cut = await RenderLoadedAsync(targets);

        await TypeAsync(cut, "1800");

        Assert.Multiple(async () =>
        {
            Assert.That((await targets.GetAsync()).Kcal, Is.EqualTo(1800));
            Assert.That(cut.FindAll("[data-testid=settings-target-error]"), Is.Empty);
        });
    }

    [Test]
    public async Task Target_Emptied_ClearsIt()
    {
        await targets.SaveAsync(2100);
        var cut = await RenderLoadedAsync(targets);
        Assert.That(TargetValue(cut), Is.EqualTo("2100"));

        await TypeAsync(cut, "");

        Assert.Multiple(async () =>
        {
            Assert.That((await targets.GetAsync()).Kcal, Is.Null);
            Assert.That(cut.FindAll("[data-testid=settings-target-error]"), Is.Empty);
        });
    }

    [TestCase("0")]
    [TestCase("-5")]
    [TestCase("1.5")]
    [TestCase("10001")]
    public async Task Target_NotAWholeNumberAboveZero_ShowsTheMessageAndSavesNothing(string typed)
    {
        await targets.SaveAsync(2100);
        var cut = await RenderLoadedAsync(targets);
        Assert.That(TargetValue(cut), Is.EqualTo("2100"));

        await TypeAsync(cut, typed);

        Assert.Multiple(async () =>
        {
            Assert.That(cut.Find("[data-testid=settings-target-error]").TextContent, Is.EqualTo(InvalidMessage));
            Assert.That(TargetValue(cut), Is.EqualTo(typed));
            Assert.That((await targets.GetAsync()).Kcal, Is.EqualTo(2100));
        });
    }

    [Test]
    public async Task Target_ValidAfterInvalid_HidesTheMessage()
    {
        var cut = await RenderLoadedAsync(targets);
        await TypeAsync(cut, "0");

        await TypeAsync(cut, "1500");

        Assert.Multiple(async () =>
        {
            Assert.That(cut.FindAll("[data-testid=settings-target-error]"), Is.Empty);
            Assert.That((await targets.GetAsync()).Kcal, Is.EqualTo(1500));
        });
    }

    [TestCase("abc")]
    [TestCase("-")]
    [TestCase(".")]
    public async Task Target_TextThatIsNotANumber_ShowsTheMessageAndKeepsTheSavedTarget(string typed)
    {
        await targets.SaveAsync(2100);
        var cut = await RenderLoadedAsync(targets);

        await TypeAsync(cut, typed);

        Assert.Multiple(async () =>
        {
            Assert.That(cut.Find("[data-testid=settings-target-error]").TextContent, Is.EqualTo(InvalidMessage));
            Assert.That(TargetValue(cut), Is.EqualTo(typed));
            Assert.That((await targets.GetAsync()).Kcal, Is.EqualTo(2100));
        });
    }

    [Test]
    public async Task Target_TextThatIsNotANumberThenEmptied_ClearsIt()
    {
        await targets.SaveAsync(2100);
        var cut = await RenderLoadedAsync(targets);
        await TypeAsync(cut, "abc");

        await TypeAsync(cut, "");

        Assert.Multiple(async () =>
        {
            Assert.That((await targets.GetAsync()).Kcal, Is.Null);
            Assert.That(cut.FindAll("[data-testid=settings-target-error]"), Is.Empty);
        });
    }

    [Test]
    public async Task Target_OlderSaveFinishesAfterANewerInvalidEntry_KeepsTheMessage()
    {
        var cut = await RenderLoadedAsync(targets);
        var field = cut.Find("[data-testid=settings-target]");

        var older = field.InputAsync("1500");
        await field.InputAsync("0");
        await older;
        await targets.GetAsync();
        await WaitForRendererAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(cut.Find("[data-testid=settings-target-error]").TextContent, Is.EqualTo(InvalidMessage));
            Assert.That(TargetValue(cut), Is.EqualTo("0"));
            Assert.That((await targets.GetAsync()).Kcal, Is.EqualTo(1500));
        });
    }

    [Test]
    public async Task Target_DatabaseHasNoSchema_ShowsEmptyOnLoadAndAfterAFailedSave_LogsNoNumber()
    {
        // Without the schema every target call fails.
        var brokenPath = Path.Combine(Path.GetTempPath(), $"settings-page-{Guid.NewGuid():N}.db3");
        await using var broken = new ServiceCollection().AddLogging().AddCore(brokenPath).BuildServiceProvider();
        try
        {
            var brokenTargets = broken.GetRequiredService<TargetService>();
            Context.Services.AddSingleton(brokenTargets);
            var cut = await RenderLoadedAsync(brokenTargets);

            await TypeAsync(cut, "1234", brokenTargets);

            // The field reads "" before the load, and shows "1234" until the failed save puts the saved target back and the page re-renders,
            // so wait for both the two log lines and the restored field.
            var expected = new[]
            {
                (UiEvent.TargetLoadFailed, LogLevel.Warning, "Reading the daily target failed"),
                (UiEvent.TargetSaveFailed, LogLevel.Warning, "Saving the daily target failed; the last saved target stays"),
            };
            cut.WaitForAssertion(() =>
            {
                Assert.That(LoggedEvents.Of(logger), Is.EqualTo(expected));
                Assert.That(TargetValue(cut), Is.EqualTo(""));
            });
            Assert.Multiple(() =>
            {
                Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Has.None.Contain("1234"));
                Assert.That(cut.FindAll("[data-testid=settings-target-error]"), Is.Empty);
            });
        }
        finally
        {
            File.Delete(brokenPath);
        }
    }

    [Test]
    public async Task Target_SaveFailsAfterALoad_PutsTheSavedTargetBackAndShowsTheHint()
    {
        await targets.SaveAsync(2100);
        var cut = await RenderLoadedAsync(targets);
        Assert.That(TargetValue(cut), Is.EqualTo("2100"));
        await TypeAsync(cut, "0");
        Assert.That(cut.Find("[data-testid=settings-target-error]").TextContent, Is.EqualTo(InvalidMessage));

        // Saves now fail while the page already holds the target it read.
        using (var other = new SQLite.SQLiteConnection(databasePath))
        {
            other.Execute("DROP TABLE AppSettings");
        }

        await TypeAsync(cut, "1234");

        cut.WaitForAssertion(() => Assert.That(TargetValue(cut), Is.EqualTo("2100")));
        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=settings-target-error]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=settings-target-hint]"), Has.Count.EqualTo(1));
            Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
            {
                (UiEvent.TargetSaveFailed, LogLevel.Warning, "Saving the daily target failed; the last saved target stays"),
            }));
            Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Has.None.Contain("1234").And.None.Contain("2100"));
        });
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
        Context.JSInterop.Setup<string>("shieldsTheme.get", _ => true).SetResult("dark");
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

        await cut.Find("[data-testid=settings-theme-light]").ChangeAsync(true);

        Assert.Multiple(() =>
        {
            Assert.That(Context.JSInterop.Invocations["shieldsTheme.set"].Single().Arguments, Is.EqualTo(new object[] { "light" }));
            Assert.That(cut.Find("[data-testid=settings-theme-light]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public async Task Theme_ReadFails_ShowsSystemAndLogsTheWarning()
    {
        var exception = new JSException("no storage");
        Context.JSInterop.Setup<string>("shieldsTheme.get", _ => true).SetException(exception);
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
        Context.JSInterop.Setup<bool>("shieldsTheme.set", _ => true).SetException(exception);
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        var control = cut.FindComponent<SegmentedControl<ThemePreference>>();
        var renders = control.RenderCount;
        await cut.Find("[data-testid=settings-theme-light]").ChangeAsync(true);

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
        Context.JSInterop.Setup<bool>("shieldsTheme.set", _ => true).SetResult(false);
        var cut = Context.Render<SettingsPage>();
        await WaitForRendererAsync();

        await cut.Find("[data-testid=settings-theme-light]").ChangeAsync(true);

        cut.WaitForAssertion(() => Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[] { (UiEvent.ThemeNotSaved, LogLevel.Warning, "Storage refused the saved theme; it applies until the app closes") })));
        Assert.That(cut.Find("[data-testid=settings-theme-light]").HasAttribute("checked"), Is.True);
    }

    [Test]
    public async Task Back_RaisesOnBack()
    {
        var backs = 0;
        var cut = Context.Render<SettingsPage>(p => p.Add(x => x.OnBack, () => backs++));
        await WaitForRendererAsync();

        await cut.Find("[data-testid=back]").ClickAsync(new());

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
