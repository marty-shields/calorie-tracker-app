using CalorieTracker.Core.Data;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Components.Layout;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Stories.Stories.Screens;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CalorieTracker.UI.Tests.Screens;

public class DashboardViewTests : ComponentTestBase
{
    private static readonly DateOnly Today = DashboardSamples.Today;

    private IRenderedComponent<DashboardView> Render(Core.Summary.DaySummary? summary, Action<ComponentParameterCollectionBuilder<DashboardView>>? more = null, DateOnly? date = null) =>
        Context.Render<DashboardView>(p =>
        {
            p.Add(x => x.Date, date ?? Today).Add(x => x.Today, Today).Add(x => x.Summary, summary);
            more?.Invoke(p);
        });

    private static string Text(IRenderedComponent<DashboardView> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static bool Exists(IRenderedComponent<DashboardView> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    [Test]
    public void Title_IsCalorieTracker()
    {
        var cut = Render(DashboardSamples.Loaded);

        Assert.That(Text(cut, "screen-title"), Is.EqualTo("Calorie Tracker"));
    }

    [Test]
    public void FirstRun_ShowsTheEmptyStateAndAPlusOnEachMealButNoMacroBarOrTotals()
    {
        var cut = Render(DashboardSamples.FirstRun);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "dashboard-empty-message"), Is.EqualTo("Nothing logged today"));
            Assert.That(Text(cut, "dashboard-empty-hint"), Is.EqualTo("Tap + on a meal to add your first food."));
            Assert.That(Text(cut, "dashboard-ring-value"), Is.EqualTo("2,100"));
            Assert.That(Text(cut, "dashboard-ring-label"), Is.EqualTo("kcal left"));
            Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("0"));
            Assert.That(Text(cut, "dashboard-target"), Is.EqualTo("2,100"));
            foreach (var meal in new[] { "breakfast", "lunch", "dinner", "snacks" })
            {
                Assert.That(Exists(cut, $"meal-{meal}-add"), Is.True, meal);
                Assert.That(Exists(cut, $"meal-{meal}-calories"), Is.False, meal);
                Assert.That(Exists(cut, $"meal-{meal}-none"), Is.False, meal);
            }

            Assert.That(Exists(cut, "dashboard-share"), Is.False);
        });
    }

    [Test]
    public void Loaded_ShowsTotalsMacrosMealsAndEntries()
    {
        var cut = Render(DashboardSamples.Loaded);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "dashboard-ring-value"), Is.EqualTo("1,338"));
            Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("762"));
            Assert.That(Exists(cut, "dashboard-share"), Is.True);
            Assert.That(Text(cut, "dashboard-share-protein"), Does.Contain("29 g"));
            Assert.That(Text(cut, "meal-breakfast-name"), Is.EqualTo("Breakfast"));
            Assert.That(Text(cut, "meal-lunch-calories"), Is.EqualTo("521 kcal"));
            Assert.That(Text(cut, "meal-dinner-none"), Is.EqualTo("Nothing logged"));
            Assert.That(cut.FindAll("[data-testid=meal-breakfast] ~ ul .row"), Has.Count.EqualTo(1));
            Assert.That(Text(cut, "entry-4"), Does.Contain("200 ml"));
            Assert.That(Text(cut, "entry-1"), Does.StartWith("Quaker Porridge oats").And.Contain("40 g").And.EndWith("149"));
        });
    }

    [Test]
    public void AnEntryAddedByServings_ShowsTheServingAndTheFoodsLabel()
    {
        var cut = Render(DashboardSamples.Loaded);

        Assert.That(Text(cut, "entry-2"), Does.Contain("1 serving · ½ pizza (201.8 g)"));
    }

    [TestCase(null, "1 serving · 40 g")]
    [TestCase("  ", "1 serving · 40 g")]
    public void AServingWithoutALabel_FallsBackToTheAmount(string? label, string expected)
    {
        var entry = new Core.Summary.SummaryEntry(9, 9, "Oats", null, FoodUnit.Gram, 40, 1, new(149, 4.4, 24, 3.4), label);
        var meals = Enum.GetValues<Meal>().Select(m => new Core.Summary.MealSummary(m, m == Meal.Breakfast ? entry.Nutrition : Core.Summary.Nutrition.Zero, m == Meal.Breakfast ? [entry] : [])).ToList();
        var day = DashboardSamples.Loaded with { Meals = meals };

        var cut = Render(day);

        Assert.That(Text(cut, "entry-9"), Does.Contain(expected));
    }

    [TestCase(null, "Weetabix", "Weetabix Original", "40 g")]
    [TestCase(null, "Ferrero, Nutella", "Nutella", "Ferrero · 40 g")]
    [TestCase(1.0, "Ferrero, Nutella", "Nutella", "Ferrero · 1 serving · 40 g")]
    public void ABrandedEntry_ShowsTheTitleAndTheBrandLineBeforeTheAmount(double? servings, string brands, string title, string detail)
    {
        var name = title == "Nutella" ? "Nutella" : "Original";
        var entry = new Core.Summary.SummaryEntry(9, 9, name, brands.Split(',')[0], FoodUnit.Gram, 40, servings, new(149, 4.4, 24, 3.4), Brands: brands);
        var meals = Enum.GetValues<Meal>().Select(m => new Core.Summary.MealSummary(m, m == Meal.Breakfast ? entry.Nutrition : Core.Summary.Nutrition.Zero, m == Meal.Breakfast ? [entry] : [])).ToList();

        var cut = Render(DashboardSamples.Loaded with { Meals = meals });

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=entry-9] .text").TextContent, Is.EqualTo(title));
            Assert.That(cut.Find("[data-testid=entry-9] .detail").TextContent, Is.EqualTo(detail));
        });
    }

    [Test]
    public void Entries_AreTappableRows()
    {
        var cut = Render(DashboardSamples.Loaded, p => p.Add(x => x.OnEdit, (EntryEditRequest _) => { }));

        Assert.That(cut.FindAll("button[data-testid^=entry-]"), Has.Count.EqualTo(3));
    }

    [TestCase(2, 22, Meal.Lunch)]
    [TestCase(4, 24, Meal.Snacks)]
    public async Task TappingAnEntry_RaisesItWithItsMealAndTheDay(int entryId, int foodId, Meal meal)
    {
        EntryEditRequest? request = null;
        var cut = Render(DashboardSamples.Loaded, p => p.Add(x => x.OnEdit, (EntryEditRequest r) => request = r), Today.AddDays(2));

        await cut.Find($"[data-testid=entry-{entryId}]").ClickAsync();

        Assert.Multiple(() =>
        {
            Assert.That((request!.Entry.EntryId, request.Entry.FoodId), Is.EqualTo((entryId, foodId)));
            Assert.That((request.Meal, request.Date), Is.EqualTo((meal, Today.AddDays(2))));
        });
    }

    [Test]
    public async Task NoTarget_ShowsCaloriesEatenAndAWayToSetOne()
    {
        var settings = 0;
        var cut = Render(DashboardSamples.NoTarget, p => p.Add(x => x.OnSettings, () => settings++));

        await cut.Find("[data-testid=dashboard-set-target]").ClickAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "dashboard-ring-value"), Is.EqualTo("762"));
            Assert.That(Text(cut, "dashboard-ring-label"), Is.EqualTo("kcal eaten"));
            Assert.That(Text(cut, "dashboard-no-target"), Is.EqualTo("No target set"));
            Assert.That(Exists(cut, "dashboard-target"), Is.False);
            Assert.That(settings, Is.EqualTo(1));
        });
    }

    [Test]
    public void OverTarget_ShowsHowManyOver()
    {
        var cut = Render(DashboardSamples.OverTarget);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "dashboard-ring-value"), Is.EqualTo("233"));
            Assert.That(Text(cut, "dashboard-ring-label"), Is.EqualTo("kcal over"));
            Assert.That(cut.Find("[data-testid=dashboard-ring]").ClassList, Does.Contain("over"));
        });
    }

    [Test]
    public void AnEmptyDayThatIsNotToday_DoesNotSayToday()
    {
        var cut = Render(DashboardSamples.FirstRun, date: Today.AddDays(-1));

        Assert.That(Text(cut, "dashboard-empty-message"), Is.EqualTo("Nothing logged this day"));
    }

    [Test]
    public void AFutureDay_LooksLikeAnyOtherDay()
    {
        var cut = Render(DashboardSamples.Loaded, date: Today.AddDays(1));

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Tomorrow"));
            Assert.That(Text(cut, "dashboard-ring-value"), Is.EqualTo("1,338"));
        });
    }

    [Test]
    public void Loading_ShowsASpinnerAndTheDateStripButNoDay()
    {
        var cut = Render(null, p => p.Add(x => x.Loading, true));

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "dashboard-loading"), Is.True);
            Assert.That(Exists(cut, "date-strip"), Is.True);
            Assert.That(Exists(cut, "dashboard-card"), Is.False);
        });
    }

    [Test]
    public async Task LoadFailed_ShowsTheErrorAndTryAgain()
    {
        var retried = 0;
        var cut = Render(null, p => p.Add(x => x.LoadFailed, true).Add(x => x.OnRetry, () => retried++));

        await cut.Find("[data-testid=dashboard-load-failed-retry]").ClickAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "dashboard-load-failed-error"), Is.EqualTo("Couldn't load this day"));
            Assert.That(retried, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task MealPlus_RaisesTheMealAndTheDayOnTheStrip()
    {
        var requests = new List<MealAddRequest>();
        var cut = Render(DashboardSamples.Loaded, p => p.Add(x => x.OnAdd, requests.Add), Today.AddDays(2));

        await cut.Find("[data-testid=meal-dinner-add]").ClickAsync();

        Assert.That(requests, Is.EqualTo(new[] { new MealAddRequest(Meal.Dinner, Today.AddDays(2)) }));
    }

    [Test]
    public async Task DateStrip_RaisesPreviousNextAndToday()
    {
        var raised = new List<string>();
        var cut = Render(DashboardSamples.Loaded, p => p
            .Add(x => x.OnPrevious, () => raised.Add("previous"))
            .Add(x => x.OnNext, () => raised.Add("next"))
            .Add(x => x.OnToday, () => raised.Add("today")));

        await cut.Find("[data-testid=date-strip-previous]").ClickAsync();
        await cut.Find("[data-testid=date-strip-next]").ClickAsync();
        await cut.Find("[data-testid=date-strip-label]").ClickAsync();

        Assert.That(raised, Is.EqualTo(new[] { "previous", "next", "today" }));
    }

    [Test]
    public void NavigationBar_ShowsTodayAsCurrentWithNoBackArrow()
    {
        var cut = Render(DashboardSamples.Loaded);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[aria-current=page]").GetAttribute("data-testid"), Is.EqualTo("today-link"));
            Assert.That(cut.FindAll("[data-testid=back]"), Is.Empty);
        });
    }

    private BunitJSModuleInterop? sheetModule;

    private void UseSheetModule()
    {
        sheetModule = Context.JSInterop.SetupModule("./_content/CalorieTracker.UI/Components/Layout/AddSheet.razor.js");
        sheetModule.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<DashboardView> OpenSheetAt(int hour, DateOnly? date = null, Action<ComponentParameterCollectionBuilder<DashboardView>>? more = null)
    {
        UseSheetModule();
        ((FakeTimeProvider)Context.Services.GetRequiredService<TimeProvider>()).SetUtcNow(new DateTimeOffset(2026, 11, 18, hour, 0, 0, TimeSpan.Zero));
        var cut = Render(DashboardSamples.Loaded, more, date);
        cut.Find("[data-testid=add-button]").Click();
        return cut;
    }

    [Test]
    public void TheSheet_IsClosedUntilAddIsTapped()
    {
        UseSheetModule();
        var cut = Render(DashboardSamples.Loaded);
        Assert.That(Exists(cut, "add-sheet"), Is.False);

        cut.Find("[data-testid=add-button]").Click();

        Assert.That(Exists(cut, "add-sheet"), Is.True);
    }

    [TestCase(8, "breakfast")]
    [TestCase(18, "dinner")]
    public void Add_SelectsTheMealOfTheTime(int hour, string meal)
    {
        var cut = OpenSheetAt(hour);

        Assert.That(cut.Find($"[data-testid=add-sheet-meal-{meal}]").HasAttribute("checked"), Is.True);
    }

    [Test]
    public void SearchFoods_RaisesTheChosenMealAndTheDayOnTheStrip_AndClosesTheSheet()
    {
        var requests = new List<MealAddRequest>();
        var day = new DateOnly(2026, 11, 12);
        var cut = OpenSheetAt(8, day, p => p.Add(x => x.OnAdd, requests.Add));

        cut.Find("[data-testid=add-sheet-meal-lunch]").Change(true);
        cut.Find("[data-testid=add-sheet-search]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(requests, Is.EqualTo(new[] { new MealAddRequest(Meal.Lunch, day) }));
            Assert.That(Exists(cut, "add-sheet"), Is.False);
        });
    }

    [Test]
    public void ScanBarcode_RaisesTheChosenMealAndTheDayOnTheStrip()
    {
        var requests = new List<MealAddRequest>();
        var day = new DateOnly(2026, 11, 12);
        var cut = OpenSheetAt(18, day, p => p.Add(x => x.OnScan, requests.Add));

        cut.Find("[data-testid=add-sheet-scan]").Click();

        Assert.That(requests, Is.EqualTo(new[] { new MealAddRequest(Meal.Dinner, day) }));
    }

    [Test]
    public async Task ScrimEscapeAndBack_CloseTheSheetWithoutNavigating_AndFocusGoesBackToAdd()
    {
        var requests = new List<MealAddRequest>();
        var cut = OpenSheetAt(12, null, p => p.Add(x => x.OnAdd, requests.Add).Add(x => x.OnScan, requests.Add));

        cut.Find("[data-testid=add-sheet-scrim]").Click();
        Assert.That(Exists(cut, "add-sheet"), Is.False, "scrim");

        cut.Find("[data-testid=add-button]").Click();
        cut.Find("[data-testid=add-sheet]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.That(Exists(cut, "add-sheet"), Is.False, "Escape");

        cut.Find("[data-testid=add-button]").Click();
        await BackButton.HandleAsync();
        cut.WaitForAssertion(() => Assert.That(Exists(cut, "add-sheet"), Is.False), TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(requests, Is.Empty);
            Assert.That(sheetModule!.Invocations["close"], Has.Count.EqualTo(3), "focus is released to Add each time");
        });
    }

    private static readonly IReadOnlyList<UsualFoodRow> UsualRows = [new(7, "Pasta", "150 g · 222 kcal")];

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void UsualPlus_AsksForTheDayOnTheStrip_AndClosesTheSheetOnlyWhenAdded(bool added, bool stillOpen)
    {
        var asked = new List<(UsualAddRequest Request, DateOnly Day)>();
        var cut = OpenSheetAt(19, Today.AddDays(-2), p => p
            .Add(x => x.UsualFoods, UsualRows)
            .Add(x => x.OnAddUsual, (request, day) =>
            {
                asked.Add((request, day));
                return Task.FromResult(added);
            }));

        cut.Find("[data-testid=add-sheet-usual-7-add]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(asked, Is.EqualTo(new[] { (new UsualAddRequest(UsualRows[0], Meal.Dinner), Today.AddDays(-2)) }));
            Assert.That(Exists(cut, "add-sheet"), Is.EqualTo(stillOpen));
        });
    }

    [Test]
    public void TheSheet_PassesTheUsualFoodsErrorAndMealChangesThrough()
    {
        var meals = new List<Meal>();
        var cut = OpenSheetAt(8, null, p => p
            .Add(x => x.UsualFoods, UsualRows)
            .Add(x => x.UsualAddError, "Couldn't add Pasta to Breakfast. Try again.")
            .Add(x => x.OnMealChanged, meals.Add));

        cut.Find("[data-testid=add-sheet-meal-lunch]").Change(true);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "add-sheet-usual-7-title"), Is.EqualTo("Pasta"));
            Assert.That(Text(cut, "add-sheet-add-error"), Is.EqualTo("Couldn't add Pasta to Breakfast. Try again."));
            Assert.That(meals, Is.EqualTo(new[] { Meal.Breakfast, Meal.Lunch }));
        });
    }

    [Test]
    public async Task SettingsGear_RaisesOnSettings()
    {
        var settings = false;
        var cut = Render(DashboardSamples.Loaded, p => p.Add(x => x.OnSettings, () => settings = true));

        await cut.Find("[data-testid=settings]").ClickAsync();

        Assert.That(settings, Is.True);
    }

    [Test]
    public void ASnackbarPostedBeforeTheScreenOpens_Shows()
    {
        Context.Services.GetRequiredService<SnackbarService>().Show("Oats added to Lunch", "Undo", () => Task.CompletedTask);

        var cut = Render(DashboardSamples.Loaded);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "snackbar-message"), Is.EqualTo("Oats added to Lunch"));
            Assert.That(Text(cut, "snackbar-action"), Is.EqualTo("Undo"));
        });
    }
}
