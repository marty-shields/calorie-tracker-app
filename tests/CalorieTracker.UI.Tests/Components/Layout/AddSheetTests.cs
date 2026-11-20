using CalorieTracker.Core.Data;
using CalorieTracker.UI.Components.Layout;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Tests.DesignSystem;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace CalorieTracker.UI.Tests.Components.Layout;

public class AddSheetTests : ComponentTestBase
{
    private const string Module = "./_content/CalorieTracker.UI/Components/Layout/AddSheet.razor.js";

    private BunitJSModuleInterop sheetModule = null!;

    [SetUp]
    public void SetUpModule()
    {
        sheetModule = Context.JSInterop.SetupModule(Module);
        sheetModule.Mode = JSRuntimeMode.Loose;
    }

    private void At(int hour, int minute = 0) =>
        ((FakeTimeProvider)Context.Services.GetRequiredService<TimeProvider>()).SetUtcNow(new DateTimeOffset(2026, 11, 18, hour, minute, 0, TimeSpan.Zero));

    private IRenderedComponent<AddSheet> Render(Action<ComponentParameterCollectionBuilder<AddSheet>>? more = null) =>
        Context.Render<AddSheet>(p =>
        {
            p.Add(x => x.Open, true);
            more?.Invoke(p);
        });

    private static string SelectedMeal(IRenderedComponent<AddSheet> cut) =>
        cut.FindAll("[data-testid^=add-sheet-meal-]").Single(r => r.HasAttribute("checked")).GetAttribute("data-testid")!["add-sheet-meal-".Length..];

    [Test]
    public void Closed_ShowsNothingAndRegistersNoBackHandler()
    {
        var cut = Context.Render<AddSheet>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup.Trim(), Is.Empty);
            Assert.That(BackButton.HasHandler, Is.False);
            Assert.That(Context.JSInterop.Invocations, Is.Empty);
        });
    }

    [Test]
    public void Open_ShowsADialogTitledAddFoodWithFourMealChipsAndTwoTiles()
    {
        At(8);
        var cut = Render();

        var sheet = cut.Find("[data-testid=add-sheet]");
        Assert.Multiple(() =>
        {
            Assert.That(sheet.GetAttribute("role"), Is.EqualTo("dialog"));
            Assert.That(sheet.GetAttribute("aria-modal"), Is.EqualTo("true"));
            Assert.That(cut.Find("#add-sheet-title").TextContent, Is.EqualTo("Add food"));
            Assert.That(sheet.GetAttribute("aria-labelledby"), Is.EqualTo("add-sheet-title"));
            Assert.That(cut.FindAll("[data-testid^=add-sheet-meal-]").Select(r => r.ParentElement!.TextContent.Trim()), Is.EqualTo(new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }));
            Assert.That(cut.Find("[data-testid=add-sheet-search]").TextContent.Trim(), Is.EqualTo("Search foods"));
            Assert.That(cut.Find("[data-testid=add-sheet-scan]").TextContent.Trim(), Is.EqualTo("Scan barcode"));
        });
    }

    [Test]
    public void Open_HasADecorativeHandleAboveTheTitle()
    {
        At(8);
        var cut = Render();

        var handle = cut.Find("[data-testid=add-sheet-handle]");

        Assert.Multiple(() =>
        {
            Assert.That(handle.GetAttribute("aria-hidden"), Is.EqualTo("true"));
            Assert.That(handle.TextContent, Is.Empty);
            Assert.That(handle.TagName, Is.EqualTo("DIV"), "not a button: it has no behaviour");
            Assert.That(handle.ParentElement!.FirstElementChild!.GetAttribute("data-testid"), Is.EqualTo("add-sheet-handle"), "first in the dialog");
            Assert.That(cut.Find("[data-testid=add-sheet]").GetAttribute("aria-labelledby"), Is.EqualTo("add-sheet-title"));
        });
    }

    [Test]
    public void Css_SizesTheHandleAndTilesFromTokens()
    {
        var css = RepoFiles.Read("src", "CalorieTracker.UI", "Components", "Layout", "AddSheet.razor.css");
        var tokens = DesignTokens.AppCss;

        Assert.Multiple(() =>
        {
            Assert.That(css, Does.Contain("width: var(--sheet-handle-width);").And.Contain("height: var(--sheet-handle-height);"));
            Assert.That(css, Does.Contain("grid-auto-rows: minmax(var(--sheet-tile-min-height), auto);").And.Contain("--icon-size: var(--sheet-tile-icon-size);"));
            Assert.That(css, Does.Not.Contain("chip-soft"), "the usual + is an IconButton Soft, not restyled here");
            foreach (var token in new[] { "--sheet-handle-width", "--sheet-handle-height", "--sheet-tile-min-height", "--sheet-tile-icon-size", "--color-chip-soft", "--color-on-chip-soft" })
            {
                Assert.That(tokens, Does.Contain(token + ":"), token);
            }

            Assert.That(tokens, Does.Match(@"--color-chip-soft: light-dark\(#[0-9a-f]{6}, #[0-9a-f]{6}\);"));
            Assert.That(tokens, Does.Match(@"--color-on-chip-soft: light-dark\(#[0-9a-f]{6}, #[0-9a-f]{6}\);"));
        });
    }

    [TestCase(8, 0, "breakfast")]
    [TestCase(12, 0, "lunch")]
    [TestCase(18, 0, "dinner")]
    [TestCase(16, 0, "snacks")]
    [TestCase(22, 0, "snacks")]
    [TestCase(4, 59, "snacks")]
    [TestCase(5, 0, "breakfast")]
    public void Open_SelectsTheMealOfTheCurrentTime(int hour, int minute, string expected)
    {
        At(hour, minute);

        var cut = Render();

        Assert.That(SelectedMeal(cut), Is.EqualTo(expected));
    }

    [Test]
    public void Clock_InPlaceOfTheRegisteredProvider_PicksTheMeal()
    {
        At(8);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 18, 0, 0, TimeSpan.Zero));

        var cut = Render(p => p.Add(x => x.Clock, clock));

        Assert.That(SelectedMeal(cut), Is.EqualTo("dinner"));
    }

    [Test]
    public void Open_UsesTheDevicesLocalTimeNotUtc()
    {
        var clock = (FakeTimeProvider)Context.Services.GetRequiredService<TimeProvider>();
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus-one", TimeSpan.FromHours(1), "plus-one", "plus-one"));
        At(10, 30);

        var cut = Render();

        Assert.That(SelectedMeal(cut), Is.EqualTo("lunch"), "UTC 10:30 is breakfast, local 11:30 is lunch");
    }

    [Test]
    public void RenderingAgainWhileOpen_KeepsThePickedMeal_AndLogsOneOpening()
    {
        var logger = new FakeLogger<AddSheet>();
        Context.Services.AddSingleton<ILogger<AddSheet>>(logger);
        At(8);
        var cut = Render();
        cut.Find("[data-testid=add-sheet-meal-dinner]").Change(true);

        At(12);
        cut.Render(p => p.Add(x => x.Open, true));

        Assert.Multiple(() =>
        {
            Assert.That(SelectedMeal(cut), Is.EqualTo("dinner"));
            Assert.That(LoggedEvents.Of(logger).Count(e => e.Id == UiEvent.AddSheetOpened), Is.EqualTo(1));
        });
    }

    [Test]
    public void ReopeningTheSheet_GuessesTheMealAgain()
    {
        At(8);
        var cut = Render();
        cut.Find("[data-testid=add-sheet-meal-dinner]").Change(true);
        Assert.That(SelectedMeal(cut), Is.EqualTo("dinner"));

        cut.Render(p => p.Add(x => x.Open, false));
        At(12);
        cut.Render(p => p.Add(x => x.Open, true));

        Assert.That(SelectedMeal(cut), Is.EqualTo("lunch"));
    }

    [Test]
    public void SearchFoods_RaisesTheChosenMeal()
    {
        At(8);
        var searched = new List<Meal>();
        var scanned = new List<Meal>();
        var cut = Render(p => p.Add(x => x.OnSearch, searched.Add).Add(x => x.OnScan, scanned.Add));

        cut.Find("[data-testid=add-sheet-meal-lunch]").Change(true);
        cut.Find("[data-testid=add-sheet-search]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(searched, Is.EqualTo(new[] { Meal.Lunch }));
            Assert.That(scanned, Is.Empty);
        });
    }

    [Test]
    public void ScanBarcode_RaisesTheChosenMeal()
    {
        At(18);
        var scanned = new List<Meal>();
        var cut = Render(p => p.Add(x => x.OnScan, scanned.Add));

        cut.Find("[data-testid=add-sheet-scan]").Click();

        Assert.That(scanned, Is.EqualTo(new[] { Meal.Dinner }));
    }

    [Test]
    public void Scrim_Closes_WithoutChoosing()
    {
        var closed = 0;
        var chosen = 0;
        var cut = Render(p => p.Add(x => x.OnClose, () => closed++).Add(x => x.OnSearch, (Meal _) => chosen++).Add(x => x.OnScan, (Meal _) => chosen++));

        cut.Find("[data-testid=add-sheet-scrim]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(chosen, Is.Zero);
        });
    }

    [Test]
    public void Escape_Closes_AndOtherKeysDoNot()
    {
        var closed = 0;
        var cut = Render(p => p.Add(x => x.OnClose, () => closed++));

        cut.Find("[data-testid=add-sheet]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.That(closed, Is.Zero);
        cut.Find("[data-testid=add-sheet]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.That(closed, Is.EqualTo(1));
    }

    [Test]
    public async Task AndroidBack_Closes()
    {
        var closed = 0;
        Render(p => p.Add(x => x.OnClose, () => closed++));

        await BackButton.HandleAsync();

        Assert.That(closed, Is.EqualTo(1));
    }

    [Test]
    public void AndroidBackHandler_IsOnlyRegisteredWhileOpen()
    {
        var cut = Render();
        Assert.That(BackButton.HasHandler, Is.True);

        cut.Render(p => p.Add(x => x.Open, false));

        Assert.That(BackButton.HasHandler, Is.False);
    }

    [Test]
    public void Opening_TrapsFocusInTheSheet_AndClosing_ReturnsItToAdd()
    {
        var cut = Render();

        var open = sheetModule.Invocations["open"].Single();
        open.Arguments[0].ShouldBeElementReferenceTo(cut.Find("[data-testid=add-sheet]"));
        Assert.That(sheetModule.Invocations["close"], Is.Empty);

        cut.Render(p => p.Add(x => x.Open, false));

        Assert.That(sheetModule.Invocations["close"], Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Disposed_WhileOpen_ReleasesFocus()
    {
        Render();

        await Context.DisposeComponentsAsync();

        Assert.That(sheetModule.Invocations["close"], Has.Count.EqualTo(1));
    }

    [Test]
    public void FocusSetUpFails_LogsItAndTheSheetStillWorks()
    {
        var logger = new FakeLogger<AddSheet>();
        Context.Services.AddSingleton<ILogger<AddSheet>>(logger);
        Context.JSInterop.Mode = JSRuntimeMode.Strict;
        var module = Context.JSInterop.SetupModule(Module);
        module.SetupVoid("open", _ => true).SetException(new JSException("Open failed"));

        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=add-sheet]"), Has.Count.EqualTo(1));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(UiEvent.AddSheetFocusFailed));
        });
    }

    [Test]
    public void Logs_OnlyTheActionAndTheMeal()
    {
        var logger = new FakeLogger<AddSheet>();
        Context.Services.AddSingleton<ILogger<AddSheet>>(logger);
        At(12);
        var cut = Render(p => p.Add(x => x.OnSearch, (Meal _) => { }).Add(x => x.OnScan, (Meal _) => { }));

        cut.Find("[data-testid=add-sheet-meal-dinner]").Change(true);
        cut.Find("[data-testid=add-sheet-search]").Click();
        cut.Find("[data-testid=add-sheet-scan]").Click();
        cut.Find("[data-testid=add-sheet-scrim]").Click();

        Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
        {
            (UiEvent.AddSheetOpened, LogLevel.Information, "Add sheet opened for Lunch"),
            (UiEvent.AddSheetChose, LogLevel.Information, "Add sheet Search for Dinner"),
            (UiEvent.AddSheetChose, LogLevel.Information, "Add sheet Scan for Dinner"),
            (UiEvent.AddSheetChose, LogLevel.Information, "Add sheet Dismiss for Dinner"),
        }));
    }

    private static readonly IReadOnlyList<UsualFoodRow> Rows =
    [
        new(7, "Pasta", "150 g · 222 kcal"),
        new(9, "Pizza", "1 serving · ½ pizza (201.8 g) · 521 kcal"),
    ];

    [Test]
    public void UsualFoods_ShowTitleAmountAndAPlusNamedForTheMeal()
    {
        At(19, 30);

        var cut = Render(p => p.Add(x => x.Usual, Rows));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=add-sheet-usual-heading]").TextContent, Is.EqualTo("Usual for dinner"));
            Assert.That(cut.Find("[data-testid=add-sheet-usual-9-title]").TextContent, Is.EqualTo("Pizza"));
            Assert.That(cut.Find("[data-testid=add-sheet-usual-9-detail]").TextContent, Is.EqualTo("1 serving · ½ pizza (201.8 g) · 521 kcal"));
            Assert.That(cut.Find("[data-testid=add-sheet-usual-7-add]").GetAttribute("aria-label"), Is.EqualTo("Add Pasta to Dinner"));
            Assert.That(cut.Find("[data-testid=add-sheet-usual-7-add]").ClassList, Does.Contain("soft"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WithNoUsualFoods_TheSectionIsHidden(bool empty)
    {
        var cut = Render(p => p.Add(x => x.Usual, empty ? [] : null));

        Assert.That(cut.FindAll("[data-testid=add-sheet-usual]"), Is.Empty);
    }

    [Test]
    public void OnMealChanged_IsRaisedWhenTheSheetOpensAndWhenAChipIsChosen()
    {
        At(12);
        var meals = new List<Meal>();

        var cut = Render(p => p.Add(x => x.OnMealChanged, meals.Add));
        cut.Find("[data-testid=add-sheet-meal-breakfast]").Change(true);
        cut.Render(p => p.Add(x => x.Open, true).Add(x => x.OnMealChanged, meals.Add));

        Assert.That(meals, Is.EqualTo(new[] { Meal.Lunch, Meal.Breakfast }));
    }

    [Test]
    public void Plus_RaisesTheFoodAndTheChosenMeal()
    {
        At(19, 30);
        var requests = new List<UsualAddRequest>();
        var cut = Render(p => p.Add(x => x.Usual, Rows).Add(x => x.OnAddUsual, requests.Add));
        cut.Find("[data-testid=add-sheet-meal-lunch]").Change(true);

        cut.Find("[data-testid=add-sheet-usual-9-add]").Click();

        Assert.That(requests, Is.EqualTo(new[] { new UsualAddRequest(Rows[1], Meal.Lunch) }));
    }

    [Test]
    public void AddError_ShowsInTheSheet_AndSearchAndScanStay()
    {
        var cut = Render(p => p.Add(x => x.Usual, Rows).Add(x => x.AddError, "Couldn't add Pasta to Dinner. Try again."));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=add-sheet-add-error]").TextContent, Is.EqualTo("Couldn't add Pasta to Dinner. Try again."));
            Assert.That(cut.Find("[data-testid=add-sheet-add-error]").GetAttribute("role"), Is.EqualTo("alert"));
            Assert.That(cut.FindAll("[data-testid=add-sheet-search]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void NoError_ShowsNoMessage() =>
        Assert.That(Render().FindAll("[data-testid=add-sheet-add-error]"), Is.Empty);
}
