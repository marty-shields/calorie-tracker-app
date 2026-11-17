using CalorieTracker.Core;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Stories.Stories.Screens;
using CalorieTracker.UI.Tests.Stories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;
using StoredServing = CalorieTracker.Core.Data.Serving;

namespace CalorieTracker.UI.Tests.Screens;

/// <summary>The page over Core's real services and a temporary database, since they have no interfaces to fake.</summary>
public class ServingPageTests : ComponentTestBase
{
    private static readonly DateOnly Day = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private ServiceProvider core = null!;
    private int pizzaId;
    private int cheddarId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"calorietracker-ui-{Guid.NewGuid():N}.db3");
        core = new ServiceCollection()
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>))
            .AddCore(databasePath)
            .BuildServiceProvider();
        await core.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        var pizza = Stored(ScreenStates.Pizza, "pizza");
        pizzaId = await Insert(pizza, new ProductServing("½ pizza (201.8 g)", 201.8));
        cheddarId = await Insert(Stored(ScreenStates.Cheddar, "cheddar"));
        Context.Services.AddSingleton(core.GetRequiredService<FoodService>());
        Context.Services.AddSingleton(core.GetRequiredService<ServingService>());
        Context.Services.AddSingleton(core.GetRequiredService<DiaryService>());
        Context.Services.AddSingleton<TimeProvider>(Clock(12, 40));
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        await core.DisposeAsync();
        File.Delete(databasePath);
    }

    private static Food Stored(Food from, string sourceId) => new()
    {
        Source = FoodSource.OpenFoodFacts,
        SourceId = sourceId,
        Name = from.Name,
        Brand = from.Brand,
        Type = from.Type,
        Unit = from.Unit,
        Kcal = from.Kcal,
        ProteinG = from.ProteinG,
        CarbsG = from.CarbsG,
        FatG = from.FatG,
        PackageAmount = from.PackageAmount,
        SearchText = from.Name.ToLowerInvariant(),
    };

    /// <summary>Stores the food and, as the lookup's write path does, its serving rows: a Product row for the serving set on it (as it is, unbounded) and a Pack row for a pack of 1000 g or ml at most.</summary>
    private async Task<int> Insert(Food food, ProductServing? serving = null)
    {
        await connection.InsertAsync(food);
        if (serving is not null)
        {
            await connection.InsertAsync(new StoredServing { FoodId = food.Id, Label = serving.Label, Amount = serving.Amount, Source = ServingSource.Product, SortOrder = 0 });
        }

        if (food.PackageAmount is > 0 and <= 1000 && food.PackageAmount != serving?.Amount)
        {
            var unit = food.Unit == FoodUnit.Millilitre ? "ml" : "g";
            await connection.InsertAsync(new StoredServing { FoodId = food.Id, Label = $"Whole pack ({food.PackageAmount:0.##} {unit})", Amount = food.PackageAmount.Value, Source = ServingSource.Pack, SortOrder = 1 });
        }

        return food.Id;
    }

    private static FakeTimeProvider Clock(int hour, int minute) => new(new DateTimeOffset(2031, 7, 19, hour, minute, 0, TimeSpan.Zero));

    private IRenderedComponent<ServingPage> Render(int foodId, Meal? meal = null, Action? onAdded = null, Action<ComponentParameterCollectionBuilder<ServingPage>>? more = null) =>
        Loaded(Context.Render<ServingPage>(p =>
        {
            p.Add(x => x.FoodId, foodId).Add(x => x.Meal, meal).Add(x => x.Date, Day);
            if (onAdded is not null)
            {
                p.Add(x => x.OnAdded, onAdded);
            }

            more?.Invoke(p);
        }));

    /// <summary>Waits until the page has read the food, so the spinner is gone.</summary>
    private static IRenderedComponent<ServingPage> Loaded(IRenderedComponent<ServingPage> cut)
    {
        cut.WaitForState(() => cut.FindAll("[data-testid=serving-loading]").Count == 0);
        return cut;
    }

    private static string Text(IRenderedComponent<ServingPage> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static bool Exists(IRenderedComponent<ServingPage> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private static bool AddDisabled(IRenderedComponent<ServingPage> cut) => cut.Find("[data-testid=serving-add]").HasAttribute("disabled");

    private async Task<IReadOnlyList<DiaryEntry>> Entries() => (await core.GetRequiredService<DiaryService>().ListAsync(Day)).Entries;

    [Test]
    public void FoodWithAServing_OpensInServingsModeAtOneServing()
    {
        var cut = Render(pizzaId, Meal.Lunch);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "screen-title"), Is.EqualTo("Casa Roma Margherita pizza"));
            Assert.That(cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("1"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 202 g"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("521 kcal"));
        });
    }

    private async Task<int> InsertMars(double? packAmount = 51, string? servingLabel = null, double? servingAmount = null)
    {
        var mars = Stored(ScreenStates.Mars, "mars");
        mars.Brands = "Mars, Mars Wrigley";
        mars.PackageLabel = "51 g";
        mars.PackageAmount = packAmount;
        return await Insert(mars, servingAmount is { } weight ? new ProductServing(servingLabel ?? "", weight) : null);
    }

    [Test]
    public async Task FoodWithOnlyAPack_OffersItAsOneServing_ShowsTheBrandLine_AndAddsItWithServingsOne()
    {
        var id = await InsertMars();
        var added = 0;
        var cut = Render(id, Meal.Snacks, () => added++);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "screen-title"), Is.EqualTo("Mars"));
            Assert.That(Text(cut, "serving-brand"), Is.EqualTo("Mars Wrigley · 51 g"));
            Assert.That(Text(cut, "serving-label"), Is.EqualTo("1 serving"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 51 g"));
            Assert.That(Text(cut, "serving-adds-detail"), Is.EqualTo("51 g of Mars"));
        });

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        var entries = await Entries();
        Assert.Multiple(() =>
        {
            Assert.That(added, Is.EqualTo(1));
            Assert.That(entries.Select(e => (e.Amount, e.Servings)), Is.EqualTo(new[] { (51d, (double?)1) }));
        });
    }

    [Test]
    public async Task FoodWithAPackAndARealServing_OffersBothWithTheRealServingFirstAndThePackClearlyLabelled()
    {
        var id = await InsertMars(51, "2 squares (20 g)", 20);
        var cut = Render(id, Meal.Snacks);

        Assert.Multiple(() =>
        {
            Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "2 squares (20 g)", "Whole pack (51 g)" }));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 20 g"));
        });
    }

    [Test]
    public async Task FoodWithNeitherAServingNorAPack_OffersNoServing()
    {
        var id = await InsertMars(null);
        var cut = Render(id, Meal.Snacks);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-label"), Is.False);
            Assert.That(Exists(cut, "serving-mode-servings"), Is.False);
        });
    }

    [Test]
    public void StartAmount_OpensAtThatWeightInGrams()
    {
        var cut = Render(cheddarId, Meal.Lunch, more: p => p.Add(x => x.StartAmount, 35));

        Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("35"));
    }

    [Test]
    public void StartAmountWithServings_OpensInServingsMode()
    {
        var cut = Render(pizzaId, Meal.Lunch, more: p => p.Add(x => x.StartAmount, 303).Add(x => x.StartServings, 1.5));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("1.5"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 303 g"));
        });
    }

    [Test]
    public void StartServings_ForAFoodWithoutAServing_OpensInGrams()
    {
        var cut = Render(cheddarId, Meal.Lunch, more: p => p.Add(x => x.StartAmount, 60).Add(x => x.StartServings, 2));

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-mode-servings"), Is.False);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("60"));
        });
    }

    [Test]
    public void StartServings_NotAboveZero_OpensInGramsWithTheAmount()
    {
        var cut = Render(pizzaId, Meal.Lunch, more: p => p.Add(x => x.StartAmount, 303).Add(x => x.StartServings, 0));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-mode-grams]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("303"));
        });
    }

    [Test]
    public void StartServings_AboveTheCarryLimit_IsHeldAtTheLimit()
    {
        var cut = Render(pizzaId, Meal.Lunch, more: p => p.Add(x => x.StartAmount, 303).Add(x => x.StartServings, 5_000_000));

        Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("1000000"));
    }

    [Test]
    public void StartAmount_NotAValidAmount_OpensAtTheDefault()
    {
        var cut = Render(cheddarId, Meal.Lunch, more: p => p.Add(x => x.StartAmount, -5));

        Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("100"));
    }

    [Test]
    public void FoodWithNoServing_OpensInGramsWithNoSwitch()
    {
        var cut = Render(cheddarId, Meal.Lunch);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-mode-servings"), Is.False);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("100"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("g"));
        });
    }

    [Test]
    public async Task FoodWithAServingOutOfRange_ShowsNoSwitch()
    {
        var long_ = new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "long",
            Name = "Long label",
            Kcal = 100,
            SearchText = "long label",
        };
        var id = await Insert(long_, new ProductServing(new string('x', FoodService.MaxServingLabelLength + 1), 50));

        var cut = Render(id, Meal.Lunch);

        Assert.That(Exists(cut, "serving-mode-servings"), Is.False);
    }

    [Test]
    public void UnknownFood_SaysNotFound() =>
        Assert.That(Text(Render(9999, Meal.Lunch), "serving-not-found-message"), Is.EqualTo("Food not found"));

    [Test]
    public async Task FoodReadFails_ShowsLoadFailedWithoutAdd()
    {
        await connection.ExecuteAsync("DROP TABLE Food");

        var cut = Render(pizzaId, Meal.Lunch);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-load-failed-error"), Is.EqualTo("Couldn't load this food"));
            Assert.That(Exists(cut, "serving-add"), Is.False);
        });
    }

    [Test]
    public async Task SwitchingModes_KeepsTheSameWeight()
    {
        var cut = Render(pizzaId, Meal.Lunch);

        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(true);
        var gramsShown = cut.Find("[data-testid=serving-amount]").GetAttribute("value");
        var kcalInGrams = Text(cut, "serving-adds-kcal");
        await cut.Find("[data-testid=serving-amount]").InputAsync("150");
        await cut.Find("[data-testid=serving-mode-servings]").ChangeAsync(true);
        var servingsShown = cut.Find("[data-testid=serving-amount]").GetAttribute("value");
        var weightInServings = Text(cut, "serving-weight");
        var kcalInServings = Text(cut, "serving-adds-kcal");
        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(true);

        Assert.Multiple(() =>
        {
            Assert.That(gramsShown, Is.EqualTo("201.8"));
            Assert.That(kcalInGrams, Is.EqualTo("521 kcal"));
            Assert.That(servingsShown, Is.EqualTo("0.74"));
            Assert.That(weightInServings, Is.EqualTo("= 150 g"));
            Assert.That(kcalInServings, Is.EqualTo("387 kcal"));
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("150"));
        });
    }

    [Test]
    public async Task SwitchingModes_WithAHugeTypedNumber_StaysInvalidInsteadOfFailing()
    {
        var cut = Render(pizzaId, Meal.Lunch);
        await cut.Find("[data-testid=serving-amount]").InputAsync("70000000000000000000000000000");

        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(true);
        var afterGrams = AddDisabled(cut);
        await cut.Find("[data-testid=serving-mode-servings]").ChangeAsync(true);

        Assert.Multiple(() =>
        {
            Assert.That(afterGrams, Is.True);
            Assert.That(AddDisabled(cut), Is.True);
            Assert.That(Exists(cut, "serving-amount-error"), Is.True);
        });
    }

    [Test]
    public async Task TypingServings_ChangesTheWeightAndWhatIsAdded()
    {
        var cut = Render(pizzaId, Meal.Lunch);

        await cut.Find("[data-testid=serving-amount]").InputAsync("2");

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 404 g"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("1041 kcal"));
        });
    }

    [Test]
    public async Task UseServing_FromGrams_SwitchesToOneServing()
    {
        var cut = Render(pizzaId, Meal.Lunch);
        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(true);

        cut.WaitForAssertion(() => cut.Find("[data-testid=serving-use-serving]"));
        await cut.Find("[data-testid=serving-use-serving]").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.That(cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), Is.True));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("1"));
        });
    }

    [TestCase("0")]
    [TestCase("-5")]
    [TestCase("")]
    [TestCase("abc")]
    [TestCase("10000.5")]
    public async Task AmountZeroBelowEmptyOrTooBig_DisablesAdd_AndShowsTheMessage(string typed)
    {
        var cut = Render(cheddarId, Meal.Lunch);

        await cut.Find("[data-testid=serving-amount]").InputAsync(typed);

        Assert.Multiple(() =>
        {
            Assert.That(AddDisabled(cut), Is.True);
            Assert.That(Exists(cut, "serving-amount-error"), Is.True);
        });
    }

    [Test]
    public async Task AmountAtTheLimit_KeepsAddEnabled()
    {
        var cut = Render(cheddarId, Meal.Lunch);

        await cut.Find("[data-testid=serving-amount]").InputAsync("10000");

        Assert.That(AddDisabled(cut), Is.False);
    }

    [TestCase("0")]
    [TestCase("10000.5")]
    public async Task InvalidAmount_Add_SavesNothing_AndShowsNoSaveError(string typed)
    {
        var added = false;
        var cut = Render(cheddarId, Meal.Lunch, () => added = true);
        await cut.Find("[data-testid=serving-amount]").InputAsync(typed);

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        Assert.Multiple(async () =>
        {
            Assert.That(await Entries(), Is.Empty);
            Assert.That(added, Is.False);
            Assert.That(Exists(cut, "serving-error"), Is.False);
        });
    }

    [Test]
    public async Task DoubleTapOnAdd_SavesOneEntry()
    {
        var cut = Render(cheddarId, Meal.Lunch);
        var add = cut.Find("[data-testid=serving-add]");

        await Task.WhenAll(add.ClickAsync(new()), add.ClickAsync(new()));

        Assert.That(await Entries(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TryAgainAfterALoadFailure_ReloadsAndShowsTheFood()
    {
        await connection.ExecuteAsync("ALTER TABLE Food RENAME TO FoodAway");
        var cut = Render(pizzaId, Meal.Lunch);
        Assert.That(Exists(cut, "serving-load-failed-error"), Is.True);
        await connection.ExecuteAsync("ALTER TABLE FoodAway RENAME TO Food");

        await cut.Find("[data-testid=serving-load-failed-retry]").ClickAsync(new());
        cut.WaitForState(() => Exists(cut, "serving-add"));

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "screen-title"), Is.EqualTo("Casa Roma Margherita pizza"));
            Assert.That(Exists(cut, "serving-load-failed-error"), Is.False);
        });
    }

    [Test]
    public void ChangedMealParameter_ForTheSameFood_ReappliesThePreset()
    {
        var cut = Render(cheddarId);
        Assert.That(Exists(cut, "serving-suggestion"), Is.True);

        cut.Render(p => p.Add(x => x.FoodId, cheddarId).Add(x => x.Meal, Meal.Dinner).Add(x => x.Date, Day));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-meal-dinner]").HasAttribute("checked"), Is.True);
            Assert.That(Text(cut, "serving-add"), Is.EqualTo("Add to Dinner"));
            Assert.That(Exists(cut, "serving-suggestion"), Is.False);
        });
    }

    [Test]
    public void ChangedFood_LoadsTheNewFood()
    {
        var cut = Render(pizzaId, Meal.Lunch);

        cut.Render(p => p.Add(x => x.FoodId, cheddarId).Add(x => x.Meal, Meal.Lunch).Add(x => x.Date, Day));
        cut.WaitForState(() => Exists(cut, "serving-add") && !Exists(cut, "serving-mode"));

        Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("100"));
    }

    [Test]
    public void PresetMeal_IsSelected_NamedOnAdd_AndShowsNoSuggestion()
    {
        var cut = Render(cheddarId, Meal.Dinner);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-meal-dinner]").HasAttribute("checked"), Is.True);
            Assert.That(Text(cut, "serving-add"), Is.EqualTo("Add to Dinner"));
            Assert.That(Exists(cut, "serving-suggestion"), Is.False);
        });
    }

    [Test]
    public void NoPresetMeal_IsSuggestedFromTheLocalTime_AndShowsTheTime()
    {
        var cut = Render(cheddarId);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-meal-lunch]").HasAttribute("checked"), Is.True);
            Assert.That(Text(cut, "serving-suggestion"), Is.EqualTo("Suggested from the time: 12:40"));
            Assert.That(Text(cut, "serving-add"), Is.EqualTo("Add to Lunch"));
        });
    }

    [TestCase(10, 59, "breakfast")]
    [TestCase(11, 0, "lunch")]
    [TestCase(14, 59, "lunch")]
    [TestCase(15, 0, "dinner")]
    [TestCase(20, 59, "dinner")]
    [TestCase(21, 0, "snacks")]
    [TestCase(0, 0, "breakfast")]
    public void SuggestedMeal_UsesTheRegisteredTimeProvider(int hour, int minute, string meal)
    {
        Context.Services.AddSingleton<TimeProvider>(Clock(hour, minute));

        var cut = Render(cheddarId);

        Assert.That(cut.Find($"[data-testid=serving-meal-{meal}]").HasAttribute("checked"), Is.True);
    }

    [Test]
    public async Task ChoosingAnotherMeal_ChangesTheAddButton()
    {
        var cut = Render(cheddarId, Meal.Lunch);

        await cut.Find("[data-testid=serving-meal-snacks]").ChangeAsync(true);

        Assert.That(Text(cut, "serving-add"), Is.EqualTo("Add to Snacks"));
    }

    [Test]
    public async Task Add_InGramsMode_SavesTheGramsForTheDayAndMeal_WithNoServings()
    {
        var added = 0;
        var cut = Render(cheddarId, Meal.Dinner, () => added++);
        await cut.Find("[data-testid=serving-amount]").InputAsync("30");

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        var entry = (await Entries()).Single();
        Assert.Multiple(() =>
        {
            Assert.That((entry.FoodId, entry.Meal, entry.Amount, entry.Servings), Is.EqualTo((cheddarId, Meal.Dinner, 30.0, (double?)null)));
            Assert.That(entry.Date, Is.EqualTo("2031-07-19"));
            Assert.That(added, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Add_InServingsMode_SavesTheWeightAndTheCount()
    {
        var cut = Render(pizzaId, Meal.Lunch);
        await cut.Find("[data-testid=serving-amount]").InputAsync("2");

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        var entry = (await Entries()).Single();
        Assert.That((entry.FoodId, entry.Meal, entry.Amount, entry.Servings), Is.EqualTo((pizzaId, Meal.Lunch, 403.6, (double?)2)));
    }

    [Test]
    public async Task Add_InGramsModeOfAFoodWithAServing_SavesNoServings()
    {
        var cut = Render(pizzaId, Meal.Lunch);
        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(true);
        await cut.Find("[data-testid=serving-amount]").InputAsync("150");

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        var entry = (await Entries()).Single();
        Assert.That((entry.Amount, entry.Servings), Is.EqualTo((150.0, (double?)null)));
    }

    [Test]
    public async Task Add_AfterSwitchingBackToServings_SavesTheExactWeightAndTheRoundedCount()
    {
        var cut = Render(pizzaId, Meal.Lunch);
        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(true);
        await cut.Find("[data-testid=serving-amount]").InputAsync("150");
        await cut.Find("[data-testid=serving-mode-servings]").ChangeAsync(true);

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        var entry = (await Entries()).Single();
        Assert.That((entry.Amount, entry.Servings), Is.EqualTo((150.0, (double?)0.74)));
    }

    [Test]
    public async Task Add_WhenSavingFails_ShowsTheErrorKeepsTheScreenAndDoesNotLeave()
    {
        var added = false;
        var cut = Render(cheddarId, Meal.Lunch, () => added = true);
        await cut.Find("[data-testid=serving-amount]").InputAsync("30");
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-error"), Is.EqualTo("Couldn't add this food. Try again."));
            Assert.That(added, Is.False);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("30"));
            Assert.That(cut.Find("[data-testid=serving-meal-lunch]").HasAttribute("checked"), Is.True);
            Assert.That(AddDisabled(cut), Is.False);
            Assert.That(cut.Find("[data-testid=serving-add]").HasAttribute("aria-busy"), Is.False);
        });
    }

    [Test]
    public async Task EditingAfterAFailedSave_ClearsTheError()
    {
        var cut = Render(cheddarId, Meal.Lunch);
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");
        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        await cut.Find("[data-testid=serving-amount]").InputAsync("40");

        Assert.That(Exists(cut, "serving-error"), Is.False);
    }

    [Test]
    public async Task Back_RaisesOnBack()
    {
        var back = false;
        var cut = Render(cheddarId, Meal.Lunch, more: p => p.Add(x => x.OnBack, () => back = true));

        await cut.Find("[data-testid=back]").ClickAsync(new());

        Assert.That(back, Is.True);
    }

    [Test]
    public async Task Add_PostsAnUndoSnackbar_AndUndoRemovesTheEntry()
    {
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var cut = Render(cheddarId, Meal.Lunch);

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());
        var message = snackbar.Current!;
        await snackbar.InvokeActionAsync(message);

        Assert.Multiple(async () =>
        {
            Assert.That((message.Text, message.ActionLabel), Is.EqualTo(("Cheddar cheese added to Lunch", "Undo")));
            Assert.That(await Entries(), Is.Empty);
            Assert.That(snackbar.Current, Is.Null);
        });
    }

    [Test]
    public async Task Add_OfABrandedFood_NamesItWithItsBrandInTheSnackbar()
    {
        var id = await InsertWeetabix();
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var cut = Render(id, Meal.Lunch);

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        Assert.That(snackbar.Current!.Text, Is.EqualTo("Weetabix Original added to Lunch"));
    }

    [Test]
    public async Task Remove_OfABrandedFood_NamesItWithItsBrandInTheSnackbar()
    {
        var id = await InsertWeetabix();
        var (cut, _) = await RenderEditing(id, Meal.Lunch, 30);

        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());

        Assert.That(Context.Services.GetRequiredService<SnackbarService>().Current!.Text, Is.EqualTo("Weetabix Original removed"));
    }

    private async Task<int> InsertWeetabix()
    {
        var food = Stored(ScreenStates.Cheddar, "weetabix");
        food.Name = "Original";
        food.Brand = "Weetabix";
        food.Brands = "Weetabix";
        return await Insert(food);
    }

    [Test]
    public async Task Add_WithAReturnRoute_PostsViewDay_AndItOpensTheDayAfterThePageIsGone()
    {
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var opened = new List<DateOnly>();
        var cut = Render(cheddarId, Meal.Lunch, more: p => p
            .Add(x => x.AfterAdd, AddedFollowUp.ViewDay)
            .Add(x => x.ViewDay, day =>
            {
                opened.Add(day);
                return Task.CompletedTask;
            }));

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());
        var message = snackbar.Current!;
        await Context.DisposeComponentsAsync();
        await snackbar.InvokeActionAsync(message);

        Assert.Multiple(async () =>
        {
            Assert.That((message.Text, message.ActionLabel), Is.EqualTo(("Cheddar cheese added to Lunch", "View day")));
            Assert.That(opened, Is.EqualTo(new[] { Day }));
            Assert.That(await Entries(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Add_AskingForViewDayWithNoWayToOpenIt_PostsUndoInstead()
    {
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var cut = Render(cheddarId, Meal.Lunch, more: p => p.Add(x => x.AfterAdd, AddedFollowUp.ViewDay));

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        Assert.That(snackbar.Current!.ActionLabel, Is.EqualTo("Undo"));
    }

    [Test]
    public async Task Undo_OfAnEntryAlreadyGone_CountsAsUndone_WithNoMessageAndNoLog()
    {
        var logger = new FakeLogger<ServingPage>();
        Context.Services.AddSingleton<ILogger<ServingPage>>(logger);
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var cut = Render(cheddarId, Meal.Lunch);
        await cut.Find("[data-testid=serving-add]").ClickAsync(new());
        var message = snackbar.Current!;
        var entry = (await Entries()).Single();
        await core.GetRequiredService<DiaryService>().RemoveAsync(entry.Id);

        await snackbar.InvokeActionAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(snackbar.Current, Is.Null);
            Assert.That(LoggedEvents.Of(logger), Is.Empty);
        });
    }

    [Test]
    public async Task Add_WhenSavingFails_PostsNoSnackbar()
    {
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var cut = Render(cheddarId, Meal.Lunch);
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        Assert.That(snackbar.Current, Is.Null);
    }

    [Test]
    public async Task Undo_WhenRemovingFails_DoesNotThrow_SaysSo_AndLogsOnlyTheStatus()
    {
        var logger = new FakeLogger<ServingPage>();
        Context.Services.AddSingleton<ILogger<ServingPage>>(logger);
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var cut = Render(cheddarId, Meal.Lunch);
        await cut.Find("[data-testid=serving-add]").ClickAsync(new());
        var message = snackbar.Current!;
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await snackbar.InvokeActionAsync(message);

        var records = logger.Collector.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snackbar.Current!.Text, Does.StartWith("Couldn't undo"));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { UiEvent.UndoFailed }));
            Assert.That(records.Select(r => r.Message), Has.None.Contain("Cheddar"));
            Assert.That(records.Select(r => r.Message), Has.None.Contain("Lunch"));
        });
    }

    /// <summary>Adds an entry to the test day and opens the page in edit mode on it, as the Dashboard's tap does.</summary>
    private async Task<(IRenderedComponent<ServingPage> Cut, int EntryId)> RenderEditing(int foodId, Meal meal, double amount, double? servings = null, Action? onSaved = null, Action? onRemoved = null)
    {
        var entry = (await core.GetRequiredService<DiaryService>().AddAsync(Day, meal, foodId, amount, servings)).Entry!;
        var cut = Loaded(Context.Render<ServingPage>(p =>
        {
            p.Add(x => x.FoodId, foodId).Add(x => x.Meal, meal).Add(x => x.Date, Day)
                .Add(x => x.EntryId, entry.Id).Add(x => x.EntryAmount, amount).Add(x => x.EntryServings, servings);
            if (onSaved is not null)
            {
                p.Add(x => x.OnSaved, onSaved);
            }

            if (onRemoved is not null)
            {
                p.Add(x => x.OnRemoved, onRemoved);
            }
        }));
        return (cut, entry.Id);
    }

    private async Task<DiaryEntry?> Kept(int id, DateOnly? day = null) => (await core.GetRequiredService<DiaryService>().ListAsync(day ?? Day)).Entries.SingleOrDefault(e => e.Id == id);

    [Test]
    public async Task Edit_OpensOnTheEntrysAmountAndMeal_WithTheEditControls()
    {
        var (cut, _) = await RenderEditing(cheddarId, Meal.Dinner, 30);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "screen-title"), Is.EqualTo("Cheddar cheese"));
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("30"));
            Assert.That(cut.Find("[data-testid=serving-meal-dinner]").HasAttribute("checked"), Is.True);
            Assert.That(Text(cut, "serving-when"), Is.EqualTo("Dinner · 19 Jul"));
            Assert.That(Text(cut, "serving-save"), Is.EqualTo("Save"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("125 kcal"));
            Assert.That(Exists(cut, "serving-suggestion"), Is.False);
            Assert.That(Exists(cut, "serving-remove"), Is.True);
        });
    }

    [Test]
    public async Task Edit_OfAnEntryAddedByServings_OpensInServingsKeepingTheExactWeight()
    {
        var (cut, id) = await RenderEditing(pizzaId, Meal.Lunch, 150, 0.74);

        var opened = (cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Text(cut, "serving-weight"));
        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        var entry = await Kept(id);
        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.EqualTo((true, "0.74", "= 150 g")));
            Assert.That((entry!.Amount, entry.Servings), Is.EqualTo((150.0, (double?)0.74)));
        });
    }

    [Test]
    public async Task Edit_OfAServingsEntryWhoseFoodLostItsServing_OpensInGrams()
    {
        var (cut, _) = await RenderEditing(cheddarId, Meal.Lunch, 150, 0.74);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-mode-servings"), Is.False);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("150"));
        });
    }

    [Test]
    public async Task Save_ChangesTheAmountAndUpdatesTheTotals_KeepingTheEntryAndItsDay()
    {
        var saved = 0;
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30, onSaved: () => saved++);
        await cut.Find("[data-testid=serving-amount]").InputAsync("45");

        Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("187 kcal"));
        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        var entry = await Kept(id);
        Assert.Multiple(() =>
        {
            Assert.That((entry!.Amount, entry.Meal, entry.Date, entry.FoodId), Is.EqualTo((45.0, Meal.Lunch, "2031-07-19", cheddarId)));
            Assert.That(saved, Is.EqualTo(1));
            Assert.That(Context.Services.GetRequiredService<SnackbarService>().Current, Is.Null);
        });
    }

    [Test]
    public async Task Save_WithAnotherMeal_MovesTheEntry()
    {
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30);
        await cut.Find("[data-testid=serving-meal-snacks]").ChangeAsync(true);

        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        Assert.That((await Kept(id))!.Meal, Is.EqualTo(Meal.Snacks));
    }

    [TestCase("0")]
    [TestCase("10000.5")]
    [TestCase("")]
    public async Task Save_WithAnInvalidAmount_ChangesNothing(string typed)
    {
        var saved = false;
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30, onSaved: () => saved = true);
        await cut.Find("[data-testid=serving-amount]").InputAsync(typed);

        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        Assert.Multiple(async () =>
        {
            Assert.That((await Kept(id))!.Amount, Is.EqualTo(30));
            Assert.That(saved, Is.False);
            Assert.That(Exists(cut, "serving-error"), Is.False);
        });
    }

    [Test]
    public async Task Save_OfAnEntryRemovedMeanwhile_SaysSo_WithoutLeavingOrThrowing()
    {
        var saved = false;
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30, onSaved: () => saved = true);
        await core.GetRequiredService<DiaryService>().RemoveAsync(id);
        await cut.Find("[data-testid=serving-amount]").InputAsync("45");

        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-gone"), Is.EqualTo("This entry was already removed."));
            Assert.That(saved, Is.False);
            Assert.That(cut.Find("[data-testid=serving-save]").HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public async Task Save_WhenTheDatabaseFails_ShowsTheErrorAndStays()
    {
        var saved = false;
        var (cut, _) = await RenderEditing(cheddarId, Meal.Lunch, 30, onSaved: () => saved = true);
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-error"), Is.EqualTo("Couldn't save this entry. Try again."));
            Assert.That(saved, Is.False);
            Assert.That(cut.Find("[data-testid=serving-save]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public async Task DoubleTapOnSave_SavesOnce()
    {
        var saved = 0;
        var (cut, _) = await RenderEditing(cheddarId, Meal.Lunch, 30, onSaved: () => saved++);
        var save = cut.Find("[data-testid=serving-save]");

        await Task.WhenAll(save.ClickAsync(new()), save.ClickAsync(new()));

        Assert.That(saved, Is.EqualTo(1));
    }

    [Test]
    public async Task Remove_RemovesTheEntry_PostsUndo_AndLeaves()
    {
        var removed = 0;
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30, onRemoved: () => removed++);

        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());

        var message = Context.Services.GetRequiredService<SnackbarService>().Current!;
        Assert.Multiple(async () =>
        {
            Assert.That(await Kept(id), Is.Null);
            Assert.That((message.Text, message.ActionLabel), Is.EqualTo(("Cheddar cheese removed", "Undo")));
            Assert.That(removed, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Undo_AfterRemove_RestoresTheEntryExactly_EvenAfterThePageIsGone()
    {
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var (cut, id) = await RenderEditing(pizzaId, Meal.Dinner, 150, 0.74);
        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());
        var message = snackbar.Current!;
        await Context.DisposeComponentsAsync();

        await snackbar.InvokeActionAsync(message);

        var entry = await Kept(id);
        Assert.Multiple(() =>
        {
            Assert.That((entry!.FoodId, entry.Meal, entry.Date, entry.Amount, entry.Servings, entry.IsRemoved), Is.EqualTo((pizzaId, Meal.Dinner, "2031-07-19", 150.0, (double?)0.74, false)));
            Assert.That(snackbar.Current, Is.Null);
        });
    }

    [Test]
    public async Task Remove_WithNoUndo_LeavesTheEntryRemoved()
    {
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30);
        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());

        Context.Services.GetRequiredService<SnackbarService>().Dismiss();

        Assert.That(await Kept(id), Is.Null);
    }

    [Test]
    public async Task Remove_OfAnEntryAlreadyGone_LeavesWithoutASnackbarOrAnError()
    {
        var removed = false;
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30, onRemoved: () => removed = true);
        await core.GetRequiredService<DiaryService>().RemoveAsync(id);

        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(Context.Services.GetRequiredService<SnackbarService>().Current, Is.Null);
            Assert.That(Exists(cut, "serving-error"), Is.False);
        });
    }

    [Test]
    public async Task Remove_WhenTheDatabaseFails_ShowsTheErrorAndStays()
    {
        var removed = false;
        var (cut, _) = await RenderEditing(cheddarId, Meal.Lunch, 30, onRemoved: () => removed = true);
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-error"), Is.EqualTo("Couldn't remove this entry. Try again."));
            Assert.That(removed, Is.False);
            Assert.That(Context.Services.GetRequiredService<SnackbarService>().Current, Is.Null);
        });
    }

    [Test]
    public async Task DoubleTapOnRemove_RemovesOnce()
    {
        var removed = 0;
        var (cut, _) = await RenderEditing(cheddarId, Meal.Lunch, 30, onRemoved: () => removed++);
        var remove = cut.Find("[data-testid=serving-remove]");

        await Task.WhenAll(remove.ClickAsync(new()), remove.ClickAsync(new()));

        Assert.That(removed, Is.EqualTo(1));
    }

    [Test]
    public async Task Undo_OfAnEntryAlreadyBack_CountsAsUndone_WithNoMessageAndNoLog()
    {
        var logger = new FakeLogger<ServingPage>();
        Context.Services.AddSingleton<ILogger<ServingPage>>(logger);
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var (cut, id) = await RenderEditing(cheddarId, Meal.Lunch, 30);
        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());
        var message = snackbar.Current!;
        await core.GetRequiredService<DiaryService>().RestoreAsync(id);

        await snackbar.InvokeActionAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(snackbar.Current, Is.Null);
            Assert.That(LoggedEvents.Of(logger), Is.Empty);
        });
    }

    [Test]
    public async Task Undo_WhenRestoringFails_DoesNotThrow_SaysSo_AndLogsOnlyTheStatus()
    {
        var logger = new FakeLogger<ServingPage>();
        Context.Services.AddSingleton<ILogger<ServingPage>>(logger);
        var snackbar = Context.Services.GetRequiredService<SnackbarService>();
        var (cut, _) = await RenderEditing(cheddarId, Meal.Lunch, 30);
        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());
        var message = snackbar.Current!;
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await snackbar.InvokeActionAsync(message);

        var records = logger.Collector.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snackbar.Current!.Text, Does.StartWith("Couldn't undo"));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { UiEvent.RestoreFailed }));
            Assert.That(records.Select(r => r.Message), Has.None.Contain("Cheddar"));
            Assert.That(records.Select(r => r.Message), Has.None.Contain("Lunch"));
            Assert.That(records.Select(r => r.Message), Has.None.Contain("30"));
        });
    }

    [Test]
    public async Task EditMode_LogsNothingOfItsOwn_OnSaveAndRemove()
    {
        var logger = new FakeLogger<ServingPage>();
        Context.Services.AddSingleton<ILogger<ServingPage>>(logger);
        var (cut, _) = await RenderEditing(cheddarId, Meal.Lunch, 30);
        await cut.Find("[data-testid=serving-save]").ClickAsync(new());
        var (second, _) = await RenderEditing(cheddarId, Meal.Lunch, 30);

        await second.Find("[data-testid=serving-remove]").ClickAsync(new());

        Assert.That(logger.Collector.GetSnapshot(), Is.Empty);
    }

    [Test]
    public async Task Edit_Back_RaisesOnBack()
    {
        var back = false;
        var entry = (await core.GetRequiredService<DiaryService>().AddAsync(Day, Meal.Lunch, cheddarId, 30)).Entry!;
        var cut = Loaded(Context.Render<ServingPage>(p => p.Add(x => x.FoodId, cheddarId).Add(x => x.Meal, Meal.Lunch).Add(x => x.Date, Day)
            .Add(x => x.EntryId, entry.Id).Add(x => x.EntryAmount, 30.0).Add(x => x.OnBack, () => back = true)));

        await cut.Find("[data-testid=back]").ClickAsync(new());

        Assert.That(back, Is.True);
    }

    [Test]
    public async Task Edit_ThenAnotherEntryOfTheSameFood_ShowsTheNewEntry_AndForgetsTheGoneNotice()
    {
        var (cut, idA) = await RenderEditing(cheddarId, Meal.Lunch, 30);
        await core.GetRequiredService<DiaryService>().RemoveAsync(idA);
        await cut.Find("[data-testid=serving-save]").ClickAsync(new());
        var goneBefore = Exists(cut, "serving-gone");
        var idB = (await core.GetRequiredService<DiaryService>().AddAsync(Day, Meal.Dinner, cheddarId, 70)).Entry!.Id;

        cut.Render(p => p.Add(x => x.EntryId, idB).Add(x => x.EntryAmount, 70.0).Add(x => x.Meal, Meal.Dinner));
        cut.WaitForState(() => cut.FindAll("[data-testid=serving-amount]").Count > 0 && cut.Find("[data-testid=serving-amount]").GetAttribute("value") == "70");

        Assert.Multiple(() =>
        {
            Assert.That(goneBefore, Is.True);
            Assert.That(Exists(cut, "serving-gone"), Is.False);
            Assert.That(Text(cut, "serving-when"), Is.EqualTo("Dinner · 19 Jul"));
            Assert.That(cut.Find("[data-testid=serving-save]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public async Task Edit_WithoutAValidAmountMealOrDay_ShowsNotFound_WithNoSaveOrTrash()
    {
        var entry = (await core.GetRequiredService<DiaryService>().AddAsync(Day, Meal.Lunch, cheddarId, 30)).Entry!;

        foreach (var (name, meal, date, amount) in new (string, Meal?, DateOnly, double?)[]
        {
            ("amount", Meal.Lunch, Day, null),
            ("meal", null, Day, 30),
            ("date", Meal.Lunch, default, 30),
        })
        {
            var cut = Loaded(Context.Render<ServingPage>(p => p.Add(x => x.FoodId, cheddarId).Add(x => x.Meal, meal).Add(x => x.Date, date)
                .Add(x => x.EntryId, entry.Id).Add(x => x.EntryAmount, amount)));

            Assert.Multiple(() =>
            {
                Assert.That(Exists(cut, "serving-not-found"), Is.True, name);
                Assert.That(Exists(cut, "serving-save"), Is.False, name);
                Assert.That(Exists(cut, "serving-remove"), Is.False, name);
            });
        }

        Assert.That((await Kept(entry.Id))!.Amount, Is.EqualTo(30.0));
    }

    [Test]
    public async Task Remove_ThenSwitchingModeOrUsingServing_ClearsTheRemoveError()
    {
        var (cut, _) = await RenderEditing(pizzaId, Meal.Lunch, 150, 0.74);
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");
        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());
        var failed = Exists(cut, "serving-error");

        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(failed, Is.True);
            Assert.That(Exists(cut, "serving-error"), Is.False);
        });
    }

    private int colaId;
    private int glassId;
    private int canId;

    /// <summary>A drink with two servings: its own glass (250 ml) and its can (330 ml, a Pack row).</summary>
    private async Task SeedCola()
    {
        var cola = Stored(ServingSamples.Cola, "cola");
        colaId = await Insert(cola, new ProductServing("1 glass (250 ml)", 250));
        var rows = await connection.QueryAsync<StoredServing>("SELECT * FROM Serving WHERE FoodId = ? ORDER BY SortOrder", colaId);
        (glassId, canId) = (rows[0].Id, rows[1].Id);
    }

    private static string[] OptionTexts(IRenderedComponent<ServingPage> cut) => [.. cut.FindAll("[data-testid=serving-select] option").Select(o => o.TextContent.Trim())];

    [Test]
    public async Task FoodWithTwoServings_OpensOnTheFirstWithAListOfBoth()
    {
        await SeedCola();

        var cut = Render(colaId, Meal.Snacks);

        Assert.Multiple(() =>
        {
            Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "1 glass (250 ml)", "Whole pack (330 ml)" }));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 250 ml"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("serving"));
        });
    }

    [Test]
    public async Task ChoosingTheOtherServing_UsesItsAmountForTheWeightAndWhatIsLogged_AndSavesItsId()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);

        await cut.Find("[data-testid=serving-select]").ChangeAsync(new() { Value = "1" });
        await cut.Find("[data-testid=serving-amount-plus]").ClickAsync(new());
        var weight = Text(cut, "serving-weight");
        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        var entry = (await Entries()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(weight, Is.EqualTo("= 660 ml"));
            Assert.That((entry.Amount, entry.Servings, entry.ServingId), Is.EqualTo((660.0, (double?)2, (int?)canId)));
        });
    }

    [Test]
    public async Task ByWeight_SavesNoServingId()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);

        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(new());
        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        Assert.That((await Entries()).Single().ServingId, Is.Null);
    }

    [Test]
    public async Task SwitchingModesAcrossAServing_KeepsTheWeight()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);
        await cut.Find("[data-testid=serving-select]").ChangeAsync(new() { Value = "1" });

        await cut.Find("[data-testid=serving-mode-grams]").ChangeAsync(new());
        var grams = cut.Find("[data-testid=serving-amount]").GetAttribute("value");
        await cut.Find("[data-testid=serving-use-serving]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(grams, Is.EqualTo("330"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 330 ml"));
        });
    }

    [Test]
    public async Task StartServingId_OpensOnThatServingWithTheSameCount()
    {
        await SeedCola();

        var cut = Render(colaId, Meal.Snacks, more: p => p.Add(x => x.StartAmount, 660).Add(x => x.StartServings, 2).Add(x => x.StartServingId, canId));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=serving-select] option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { false, true }));
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("2"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 660 ml"));
        });
    }

    [Test]
    public async Task NoServingNamed_OpensOnTheSmallest_EvenWhenItWasAddedLast()
    {
        await SeedCola();
        await connection.ExecuteAsync("INSERT INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (?, 'My sip', 100, 2, 2)", colaId);

        var cut = Render(colaId, Meal.Snacks);

        Assert.Multiple(() =>
        {
            Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "My sip", "1 glass (250 ml)", "Whole pack (330 ml)" }));
            Assert.That(cut.FindAll("[data-testid=serving-select] option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { true, false, false }));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 100 ml"));
        });
    }

    [TestCase(null)]
    [TestCase(99999)]
    public async Task StartServingId_NullOrOneTheFoodDoesNotHave_OpensOnTheSmallestServing(int? unknown)
    {
        await SeedCola();

        var cut = Render(colaId, Meal.Snacks, more: p => p.Add(x => x.StartAmount, 500).Add(x => x.StartServings, 2).Add(x => x.StartServingId, unknown));

        Assert.That(cut.FindAll("[data-testid=serving-select] option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public async Task Edit_OfAnEntryAddedByAServing_OpensOnThatServing_AndSaveKeepsOrChangesIt()
    {
        await SeedCola();
        var entry = (await core.GetRequiredService<DiaryService>().AddAsync(Day, Meal.Snacks, colaId, 330, 1, canId)).Entry!;
        var cut = Loaded(Context.Render<ServingPage>(p => p.Add(x => x.FoodId, colaId).Add(x => x.Meal, Meal.Snacks).Add(x => x.Date, Day)
            .Add(x => x.EntryId, entry.Id).Add(x => x.EntryAmount, 330.0).Add(x => x.EntryServings, 1.0).Add(x => x.EntryServingId, canId)));
        var opened = cut.FindAll("[data-testid=serving-select] option").Select(o => o.HasAttribute("selected")).ToArray();

        await cut.Find("[data-testid=serving-select]").ChangeAsync(new() { Value = "0" });
        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        var saved = await Kept(entry.Id);
        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.EqualTo(new[] { false, true }));
            Assert.That((saved!.Amount, saved.Servings, saved.ServingId), Is.EqualTo((250.0, (double?)1, (int?)glassId)));
        });
    }

    [TestCase(null)]
    [TestCase(99999)]
    public async Task Edit_OfAnEntryWhoseServingTheFoodDoesNotOffer_KeepsItsCountAndLinkOnSave(int? unknown)
    {
        await SeedCola();
        var entry = (await core.GetRequiredService<DiaryService>().AddAsync(Day, Meal.Snacks, colaId, 500, 2, null)).Entry!;
        var cut = Loaded(Context.Render<ServingPage>(p => p.Add(x => x.FoodId, colaId).Add(x => x.Meal, Meal.Snacks).Add(x => x.Date, Day)
            .Add(x => x.EntryId, entry.Id).Add(x => x.EntryAmount, 500.0).Add(x => x.EntryServings, 2.0).Add(x => x.EntryServingId, unknown)));
        await cut.Find("[data-testid=serving-save]").ClickAsync(new());

        var saved = await Kept(entry.Id);
        Assert.Multiple(() =>
        {
            Assert.That((saved!.Amount, saved.Servings, saved.ServingId), Is.EqualTo((500.0, (double?)2, (int?)null)));
        });
    }

    [Test]
    public async Task AddingAnOwnServing_WhenTheReloadFails_ShowsTheLoadError()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);
        await cut.Find("[data-testid=serving-own-add]").ClickAsync(new());
        await cut.Find("[data-testid=serving-own-label]").InputAsync(new() { Value = "My mug" });
        await cut.Find("[data-testid=serving-own-amount]").InputAsync(new() { Value = "200" });
        // The serving is written, then the food can no longer be read (a fetch time that is no date), as when the database fails between the two calls.
        await connection.ExecuteAsync("CREATE TRIGGER BreakFood AFTER INSERT ON Serving BEGIN UPDATE Food SET FetchedAt = -1 WHERE Id = NEW.FoodId; END");

        await cut.Find("[data-testid=serving-own-save]").ClickAsync(new());

        Assert.That(Text(cut, "serving-load-failed-error"), Is.EqualTo("Couldn't load this food"));
    }

    [Test]
    public async Task AddingAnOwnServing_SavesItAsAUserRow_AndOpensOnItCountingOne()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);

        await cut.Find("[data-testid=serving-own-add]").ClickAsync(new());
        await cut.Find("[data-testid=serving-own-label]").InputAsync(new() { Value = "My mug" });
        await cut.Find("[data-testid=serving-own-amount]").InputAsync(new() { Value = "200" });
        await cut.Find("[data-testid=serving-own-save]").ClickAsync(new());

        var rows = await connection.QueryAsync<StoredServing>("SELECT * FROM Serving WHERE FoodId = ? AND Source = ?", colaId, (int)ServingSource.User);
        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(r => (r.Label, r.Amount)), Is.EqualTo(new[] { ("My mug", 200.0) }));
            Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "My mug", "1 glass (250 ml)", "Whole pack (330 ml)" }));
            Assert.That(cut.FindAll("[data-testid=serving-select] option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { true, false, false }));
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("1"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 200 ml"));
            Assert.That(Exists(cut, "serving-own-label"), Is.False);
        });
    }

    [Test]
    public async Task AddingAnOwnServing_ToAFoodWithNone_TurnsOnServings()
    {
        var cut = Render(cheddarId, Meal.Lunch);

        await cut.Find("[data-testid=serving-own-add]").ClickAsync(new());
        await cut.Find("[data-testid=serving-own-label]").InputAsync(new() { Value = "1 slice" });
        await cut.Find("[data-testid=serving-own-amount]").InputAsync(new() { Value = "28" });
        await cut.Find("[data-testid=serving-own-save]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-label"), Is.EqualTo("1 slice"));
            Assert.That(cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), Is.True);
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 28 g"));
        });
    }

    [Test]
    public async Task AddingAnOwnServing_OfAnAmountTheFoodAlreadyHasOfItsOwn_SaysSoAndKeepsTheForm()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);
        async Task AddOwn(string label)
        {
            await cut.Find("[data-testid=serving-own-add]").ClickAsync(new());
            await cut.Find("[data-testid=serving-own-label]").InputAsync(new() { Value = label });
            await cut.Find("[data-testid=serving-own-amount]").InputAsync(new() { Value = "200" });
            await cut.Find("[data-testid=serving-own-save]").ClickAsync(new());
        }

        await AddOwn("My mug");
        await AddOwn("Big mug");

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-own-error"), Is.EqualTo("You already have a serving of that size."));
            Assert.That(cut.Find("[data-testid=serving-own-label]").GetAttribute("value"), Is.EqualTo("Big mug"));
        });
    }

    [Test]
    public async Task OwnForm_CancelAndEditing_ClearTheError_AndCancelHidesTheForm()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);
        await cut.Find("[data-testid=serving-own-add]").ClickAsync(new());

        await cut.Find("[data-testid=serving-own-cancel]").ClickAsync(new());

        Assert.That(Exists(cut, "serving-own-label"), Is.False);
    }

    [Test]
    public async Task OwnForm_AFailedSave_SaysSo()
    {
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);
        await cut.Find("[data-testid=serving-own-add]").ClickAsync(new());
        await cut.Find("[data-testid=serving-own-label]").InputAsync(new() { Value = "My mug" });
        await cut.Find("[data-testid=serving-own-amount]").InputAsync(new() { Value = "200" });
        await connection.ExecuteAsync("DROP TABLE Serving");

        await cut.Find("[data-testid=serving-own-save]").ClickAsync(new());

        Assert.That(Text(cut, "serving-own-error"), Is.EqualTo("Couldn't save this serving. Try again."));
    }

    [Test]
    public async Task DeletingAnOwnServing_RemovesIt_KeepsItsEntriesAndOpensOnTheFirstServing()
    {
        await SeedCola();
        var own = (await core.GetRequiredService<ServingService>().AddAsync(colaId, "My mug", 200)).Serving!;
        var entry = (await core.GetRequiredService<DiaryService>().AddAsync(Day, Meal.Snacks, colaId, 400, 2, own.Id)).Entry!;
        var cut = Render(colaId, Meal.Snacks, more: p => p.Add(x => x.StartAmount, 400).Add(x => x.StartServings, 2).Add(x => x.StartServingId, own.Id));
        var before = cut.Find("[data-testid=serving-own-delete]").TextContent.Trim();

        await cut.Find("[data-testid=serving-own-delete]").ClickAsync(new());

        var kept = await Kept(entry.Id);
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo("Delete serving: My mug"));
            Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "1 glass (250 ml)", "Whole pack (330 ml)" }));
            Assert.That(cut.FindAll("[data-testid=serving-select] option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { true, false }));
            Assert.That(Exists(cut, "serving-own-delete"), Is.False);
            Assert.That((kept!.Amount, kept.Servings, kept.ServingId), Is.EqualTo((400.0, (double?)2, (int?)null)));
        });
    }

    [Test]
    public async Task DeletingTheOnlyServing_FallsBackToGramsAtTheSameWeight()
    {
        var own = (await core.GetRequiredService<ServingService>().AddAsync(cheddarId, "1 slice", 28)).Serving!;
        var cut = Render(cheddarId, Meal.Lunch, more: p => p.Add(x => x.StartAmount, 56).Add(x => x.StartServings, 2).Add(x => x.StartServingId, own.Id));

        await cut.Find("[data-testid=serving-own-delete]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-mode"), Is.False);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("56"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("g"));
        });
    }

    [Test]
    public async Task OwnServingActions_LogNothingTheScreenOwns_AndNothingTyped()
    {
        var logger = new FakeLogger<ServingPage>();
        Context.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ServingPage>>(logger);
        await SeedCola();
        var cut = Render(colaId, Meal.Snacks);
        await cut.Find("[data-testid=serving-own-add]").ClickAsync(new());
        await cut.Find("[data-testid=serving-own-label]").InputAsync(new() { Value = "Grandma's secret mug" });
        await cut.Find("[data-testid=serving-own-amount]").InputAsync(new() { Value = "123.4" });
        await cut.Find("[data-testid=serving-own-save]").ClickAsync(new());

        Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Has.None.Contain("Grandma").And.None.Contain("123"));
    }
}
