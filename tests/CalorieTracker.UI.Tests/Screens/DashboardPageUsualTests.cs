using CalorieTracker.Core;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Summary;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Shields.UI.Components.Feedback;
using SQLite;

namespace CalorieTracker.UI.Tests.Screens;

/// <summary>The Add sheet's usual foods, through the page over Core's real services and a temporary database.</summary>
public class DashboardPageUsualTests : ComponentTestBase
{
    private static readonly DateOnly Today = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private ServiceProvider core = null!;
    private FakeLogger<DashboardPage> logger = null!;
    private SnackbarService snackbar => Context.Services.GetRequiredService<SnackbarService>();

    [SetUp]
    public async Task SetUp()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"calorietracker-ui-{Guid.NewGuid():N}.db3");
        core = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(databasePath)
            .BuildServiceProvider();
        await core.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        Context.JSInterop.SetupModule("./_content/Shields.UI/Components/Overlays/Sheet.razor.js").Mode = JSRuntimeMode.Loose;
        logger = new FakeLogger<DashboardPage>();
        Context.Services.AddSingleton<ILogger<DashboardPage>>(logger);
        foreach (var service in new object[] { core.GetRequiredService<DaySummaryService>(), core.GetRequiredService<DiaryService>(), core.GetRequiredService<FoodHistoryService>(), core.GetRequiredService<FoodService>() })
        {
            Context.Services.AddSingleton(service.GetType(), service);
        }

        At(19, 30);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        await core.DisposeAsync();
        File.Delete(databasePath);
    }

    private void At(int hour, int minute) =>
        ((FakeTimeProvider)Context.Services.GetRequiredService<TimeProvider>()).SetUtcNow(new DateTimeOffset(2031, 7, 19, hour, minute, 0, TimeSpan.Zero));

    private async Task<Food> Food(string name, double kcal, double? servingGrams = null, string? servingLabel = null, FoodUnit unit = FoodUnit.Gram)
    {
        var food = new Food { Source = FoodSource.OpenFoodFacts, SourceId = name, Name = name, Type = FoodType.Ingredient, Unit = unit, Kcal = kcal, ProteinG = 1, CarbsG = 1, FatG = 1, SearchText = name.ToLowerInvariant() };
        await connection.InsertAsync(food);
        if (servingGrams is { } grams)
        {
            await connection.InsertAsync(new Serving { FoodId = food.Id, Label = servingLabel!, Amount = grams, Source = ServingSource.User });
        }

        return food;
    }

    private Task<DiaryResult> Log(DateOnly day, Meal meal, Food food, double grams, double? servings = null, int? servingId = null) =>
        core.GetRequiredService<DiaryService>().AddAsync(day, meal, food.Id, grams, servings, servingId);

    private IRenderedComponent<DashboardPage> OpenSheet()
    {
        var cut = Context.Render<DashboardPage>();
        cut.WaitForState(() => cut.FindAll("[data-testid=dashboard-loading]").Count == 0);
        cut.Find("[data-testid=add-button]").Click();
        cut.WaitForState(() => cut.FindAll("[data-testid=add-sheet]").Count == 1);
        return cut;
    }

    /// <summary>Finds and taps in one dispatcher turn, so a re-render of the page between the two cannot drop the handler.</summary>
    private static Task Tap(IRenderedComponent<DashboardPage> cut, string selector) =>
        cut.InvokeAsync(() => cut.Find(selector).ClickAsync(new()));

    private static bool Exists(IRenderedComponent<DashboardPage> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private static string Text(IRenderedComponent<DashboardPage> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static IEnumerable<string> UsualTitles(IRenderedComponent<DashboardPage> cut) =>
        cut.FindAll("[data-testid^=add-sheet-usual-][data-testid$=-title]").Select(e => e.TextContent.Trim());

    [Test]
    public async Task AtDinnerTime_ListsUpToThreeUsualDinnerFoods_EachWithItsLastAmountAndKcal()
    {
        var pasta = await Food("Pasta", 148);
        var rice = await Food("Rice", 130);
        var fish = await Food("Fish", 100);
        var peas = await Food("Peas", 80);
        foreach (var food in new[] { pasta, rice, fish, peas })
        {
            await Log(Today.AddDays(-1), Meal.Dinner, food, 150);
        }

        await Log(Today.AddDays(-2), Meal.Dinner, pasta, 150);
        await Log(Today.AddDays(-2), Meal.Dinner, rice, 150);
        await Log(Today.AddDays(-3), Meal.Dinner, pasta, 150);

        var cut = OpenSheet();

        cut.WaitForAssertion(() =>
        {
            Assert.That(Text(cut, "add-sheet-usual-heading"), Is.EqualTo("Usual for dinner"));
            Assert.That(cut.FindAll("[data-testid^=add-sheet-usual-][data-testid$=-add]"), Has.Count.EqualTo(3));
            Assert.That(UsualTitles(cut), Is.EqualTo(new[] { "Pasta", "Rice", "Peas" }));
            Assert.That(Text(cut, $"add-sheet-usual-{pasta.Id}-detail"), Is.EqualTo("150 g · 222 kcal"));
        });
    }

    [Test]
    public async Task ARowAddedByServings_ShowsTheServingAndItsKcal()
    {
        var pizza = await Food("Pizza", 258, 201.8, "½ pizza (201.8 g)");
        var servingId = await connection.ExecuteScalarAsync<int>("SELECT Id FROM Serving WHERE FoodId = ?", pizza.Id);
        await Log(Today.AddDays(-1), Meal.Dinner, pizza, 201.8, 1, servingId);

        var cut = OpenSheet();

        cut.WaitForAssertion(() => Assert.That(Text(cut, $"add-sheet-usual-{pizza.Id}-detail"), Is.EqualTo("1 serving · ½ pizza (201.8 g) · 521 kcal")));
    }

    [Test]
    public async Task EachPlus_HasANameSayingWhatItAdds()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);

        var cut = OpenSheet();

        cut.WaitForAssertion(() => Assert.That(cut.Find($"[data-testid=add-sheet-usual-{pasta.Id}-add]").GetAttribute("aria-label"), Is.EqualTo("Add Pasta to Dinner")));
    }

    [Test]
    public async Task ChoosingTheBreakfastChip_ShowsTheUsualBreakfastFoods()
    {
        var pasta = await Food("Pasta", 148);
        var oats = await Food("Oats", 370);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        await Log(Today.AddDays(-1), Meal.Breakfast, oats, 40);
        var cut = OpenSheet();
        cut.WaitForState(() => UsualTitles(cut).SequenceEqual(new[] { "Pasta" }));

        await cut.InvokeAsync(() => cut.Find("[data-testid=add-sheet-meal-breakfast]").Change(true));

        cut.WaitForAssertion(() =>
        {
            Assert.That(Text(cut, "add-sheet-usual-heading"), Is.EqualTo("Usual for breakfast"));
            Assert.That(UsualTitles(cut), Is.EqualTo(new[] { "Oats" }));
            Assert.That(Text(cut, $"add-sheet-usual-{oats.Id}-detail"), Is.EqualTo("40 g · 148 kcal"));
        });
    }

    [Test]
    public async Task WithNoUsualFoodsForTheMeal_TheSectionIsHidden_AndSearchAndScanStay()
    {
        await Log(Today.AddDays(-1), Meal.Breakfast, await Food("Oats", 370), 40);

        var cut = OpenSheet();
        cut.WaitForAssertion(() => Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Does.Contain("Usual foods for the Add sheet read for Dinner, rows 0")));

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "add-sheet-usual"), Is.False);
            Assert.That(Exists(cut, "add-sheet-search"), Is.True);
            Assert.That(Exists(cut, "add-sheet-scan"), Is.True);
        });
    }

    [Test]
    public async Task AFailedRead_HidesTheSection_AndTheRestOfTheSheetStillWorks()
    {
        await Log(Today.AddDays(-1), Meal.Dinner, await Food("Pasta", 148), 150);
        await connection.ExecuteAsync("ALTER TABLE DiaryEntry RENAME TO DiaryEntryAway");
        var requests = new List<MealAddRequest>();
        var scans = new List<MealAddRequest>();
        var cut = Context.Render<DashboardPage>(p => p.Add(x => x.OnAdd, requests.Add).Add(x => x.OnScan, scans.Add));
        cut.WaitForState(() => cut.FindAll("[data-testid=dashboard-loading]").Count == 0);
        await Tap(cut, "[data-testid=add-button]");
        cut.WaitForState(() => LoggedEvents.Of(logger).Any(e => e.Id == UiEvent.UsualReadFailed), TimeSpan.FromSeconds(10));
        Assert.That(Exists(cut, "add-sheet-search"), Is.True);

        await Tap(cut, "[data-testid=add-sheet-search]");
        cut.WaitForState(() => !Exists(cut, "add-sheet"));
        cut.Find("[data-testid=add-button]").Click();
        cut.WaitForState(() => Exists(cut, "add-sheet-scan"));
        await Tap(cut, "[data-testid=add-sheet-scan]");

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "add-sheet-usual"), Is.False);
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Meal, Is.EqualTo(Meal.Dinner));
            Assert.That(scans, Has.Count.EqualTo(1));
            Assert.That(scans[0].Meal, Is.EqualTo(Meal.Dinner));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(UiEvent.UsualReadFailed));
        });
    }

    [Test]
    public async Task Plus_AddsTheFoodWithItsLastAmountToTheChosenMealAndDay_ClosesTheSheet_AndOffersUndo()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));

        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");

        cut.WaitForAssertion(() => Assert.That(Exists(cut, "add-sheet"), Is.False));
        var added = (await core.GetRequiredService<DiaryService>().ListAsync(Today)).Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(added.Meal, Is.EqualTo(Meal.Dinner));
            Assert.That(added.FoodId, Is.EqualTo(pasta.Id));
            Assert.That(added.Amount, Is.EqualTo(150));
            Assert.That(snackbar.Current!.Text, Is.EqualTo("Pasta added to Dinner"));
            Assert.That(snackbar.Current.ActionLabel, Is.EqualTo("Undo"));
        });
        cut.WaitForAssertion(() => Assert.That(Text(cut, "meal-dinner-total"), Is.EqualTo("222 kcal")));
    }

    [Test]
    public async Task Plus_OnAServingRow_KeepsTheServingsAndTheServingId()
    {
        var pizza = await Food("Pizza", 258, 201.8, "½ pizza (201.8 g)");
        var servingId = await connection.ExecuteScalarAsync<int>("SELECT Id FROM Serving WHERE FoodId = ?", pizza.Id);
        await Log(Today.AddDays(-1), Meal.Dinner, pizza, 403.6, 2, servingId);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pizza.Id}-add"));
        Assert.That(Text(cut, $"add-sheet-usual-{pizza.Id}-detail"), Is.EqualTo("2 servings · ½ pizza (201.8 g) · 1,041 kcal"));

        await Tap(cut, $"[data-testid=add-sheet-usual-{pizza.Id}-add]");

        var added = (await core.GetRequiredService<DiaryService>().ListAsync(Today)).Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(added.Amount, Is.EqualTo(403.6));
            Assert.That(added.Servings, Is.EqualTo(2));
            Assert.That(added.ServingId, Is.EqualTo(servingId));
        });
    }

    [Test]
    public async Task Plus_OnAnotherDayOnTheStrip_AddsToThatDay()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        var cut = Context.Render<DashboardPage>(p => p.Add(x => x.Date, Today.AddDays(-3)));
        cut.WaitForState(() => cut.FindAll("[data-testid=dashboard-loading]").Count == 0);
        cut.Find("[data-testid=add-button]").Click();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));

        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");

        Assert.That((await core.GetRequiredService<DiaryService>().ListAsync(Today.AddDays(-3))).Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Undo_RemovesTheEntry()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));
        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");

        await snackbar.InvokeActionAsync(snackbar.Current!);

        Assert.That((await core.GetRequiredService<DiaryService>().ListAsync(Today)).Entries, Is.Empty);
    }

    [Test]
    public async Task Undo_WhenRemovingFails_DoesNotThrow_SaysSo_AndLogsOnlyTheStatus()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));
        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");
        var message = snackbar.Current!;
        cut.WaitForAssertion(() => Assert.That(Text(cut, "meal-dinner-total"), Is.EqualTo("222 kcal")));
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await snackbar.InvokeActionAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(snackbar.Current!.Text, Does.StartWith("Couldn't undo"));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(UiEvent.UsualUndoFailed));
        });
    }

    [Test]
    public async Task AFailedAdd_KeepsTheSheetOpen_SaysWhatFailed_AndPostsNoSnackbar()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "add-sheet"), Is.True);
            Assert.That(Text(cut, "add-sheet-add-error"), Is.EqualTo("Couldn't add Pasta to Dinner. Try again."));
            Assert.That(snackbar.Current, Is.Null);
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(UiEvent.UsualAddFailed));
        });
    }

    [Test]
    public async Task LogLines_CarryFoodIdsMealsAndCountsOnly_NeverAFoodNameOrAmount()
    {
        var pasta = await Food("Pasta Carbonara", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 157);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));
        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");
        cut.WaitForAssertion(() => Assert.That(Text(cut, "meal-dinner-total"), Is.EqualTo("232 kcal")));
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");
        await snackbar.InvokeActionAsync(snackbar.Current!);

        var messages = logger.Collector.GetSnapshot().Select(r => r.Message).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { UiEvent.UsualRead, UiEvent.UsualAdded, UiEvent.UsualUndoFailed }));
            Assert.That(messages, Does.Contain("Usual food " + pasta.Id + " added to Dinner"));
            Assert.That(messages, Has.None.Contain("Pasta").And.None.Contain("Carbonara").And.None.Contain("157").And.None.Contain("kcal"));
        });
    }

    [Test]
    public async Task LogLines_OfAFailedAdd_CarryNoFoodNameOrAmount()
    {
        var pasta = await Food("Pasta Carbonara", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 157);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");

        var messages = logger.Collector.GetSnapshot().Select(r => r.Message).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(UiEvent.UsualAddFailed));
            Assert.That(messages, Has.None.Contain("Pasta").And.None.Contain("Carbonara").And.None.Contain("157").And.None.Contain("kcal"));
        });
    }

    [Test]
    public async Task LogLines_OfAFailedRead_CarryNoFoodNameOrAmount()
    {
        await Log(Today.AddDays(-1), Meal.Dinner, await Food("Pasta Carbonara", 148), 157);
        await connection.ExecuteAsync("ALTER TABLE DiaryEntry RENAME TO DiaryEntryAway");
        var cut = Context.Render<DashboardPage>();
        cut.WaitForState(() => cut.FindAll("[data-testid=dashboard-loading]").Count == 0);
        await Tap(cut, "[data-testid=add-button]");
        cut.WaitForState(() => LoggedEvents.Of(logger).Any(e => e.Id == UiEvent.UsualReadFailed), TimeSpan.FromSeconds(10));
        Assert.That(Exists(cut, "add-sheet-search"), Is.True);

        var messages = logger.Collector.GetSnapshot().Select(r => r.Message).ToList();
        Assert.That(messages, Has.None.Contain("Pasta").And.None.Contain("Carbonara").And.None.Contain("157").And.None.Contain("kcal"));
    }

    [Test]
    public async Task Plus_AddsToTheMealOfTheChosenChip()
    {
        var oats = await Food("Oats", 370);
        var soup = await Food("Soup", 50);
        await Log(Today.AddDays(-1), Meal.Dinner, oats, 40);
        await Log(Today.AddDays(-1), Meal.Lunch, soup, 300);
        var cut = OpenSheet();
        cut.WaitForState(() => UsualTitles(cut).SequenceEqual(new[] { "Oats" }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-sheet-meal-lunch]").Change(true));
        cut.WaitForState(() => UsualTitles(cut).SequenceEqual(new[] { "Soup" }));

        await Tap(cut, $"[data-testid=add-sheet-usual-{soup.Id}-add]");

        var added = (await core.GetRequiredService<DiaryService>().ListAsync(Today)).Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(added.Meal, Is.EqualTo(Meal.Lunch));
            Assert.That(snackbar.Current!.Text, Is.EqualTo("Soup added to Lunch"));
        });
    }

    [Test]
    public async Task ARowOfAMillilitreFood_ShowsMl_AndOneWhoseServingWasDeleted_FallsBackToTheAmount()
    {
        var milk = await Food("Milk", 46, unit: FoodUnit.Millilitre);
        var pizza = await Food("Pizza", 258, 201.8, "½ pizza (201.8 g)");
        var servingId = await connection.ExecuteScalarAsync<int>("SELECT Id FROM Serving WHERE FoodId = ?", pizza.Id);
        await Log(Today.AddDays(-1), Meal.Dinner, milk, 200);
        await Log(Today.AddDays(-1), Meal.Dinner, pizza, 201.8, 1, servingId);
        await connection.ExecuteAsync("DELETE FROM Serving WHERE Id = ?", servingId);

        var cut = OpenSheet();

        cut.WaitForAssertion(() =>
        {
            Assert.That(Text(cut, $"add-sheet-usual-{milk.Id}-detail"), Is.EqualTo("200 ml · 92 kcal"));
            Assert.That(Text(cut, $"add-sheet-usual-{pizza.Id}-detail"), Is.EqualTo("1 serving · 201.8 g · 521 kcal"));
        });
    }

    [Test]
    public async Task SwitchingChipsTwiceWithoutWaiting_ShowsTheLastMealOnly()
    {
        var oats = await Food("Oats", 370);
        var soup = await Food("Soup", 50);
        await Log(Today.AddDays(-1), Meal.Breakfast, oats, 40);
        await Log(Today.AddDays(-1), Meal.Lunch, soup, 300);
        var cut = OpenSheet();

        await cut.InvokeAsync(() => cut.Find("[data-testid=add-sheet-meal-lunch]").Change(true));
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-sheet-meal-breakfast]").Change(true));

        cut.WaitForState(() => UsualTitles(cut).SequenceEqual(new[] { "Oats" }));
        Assert.That(Text(cut, "add-sheet-usual-heading"), Is.EqualTo("Usual for breakfast"));
    }

    [Test]
    public async Task Undo_OfAnEntryAlreadyRemoved_SaysNothingAndLogsNothing()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));
        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");
        var message = snackbar.Current!;
        var diary = core.GetRequiredService<DiaryService>();
        await diary.RemoveAsync((await diary.ListAsync(Today)).Entries.Single().Id);

        await snackbar.InvokeActionAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(snackbar.Current?.Text ?? "", Does.Not.StartWith("Couldn't undo"));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Not.Contain(UiEvent.UsualUndoFailed));
        });
    }

    [Test]
    public async Task AFailedAddFollowedByAChipChange_ClearsTheError()
    {
        var pasta = await Food("Pasta", 148);
        await Log(Today.AddDays(-1), Meal.Dinner, pasta, 150);
        var cut = OpenSheet();
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{pasta.Id}-add"));
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");
        await Tap(cut, $"[data-testid=add-sheet-usual-{pasta.Id}-add]");
        cut.WaitForState(() => Exists(cut, "add-sheet-add-error"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=add-sheet-meal-breakfast]").Change(true));

        cut.WaitForAssertion(() => Assert.That(Exists(cut, "add-sheet-add-error"), Is.False));
    }
}
