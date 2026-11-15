using CalorieTracker.Core;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Tests.Stories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SQLite;

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
        pizzaId = await Insert(Stored(ScreenStates.Pizza, "pizza"));
        cheddarId = await Insert(Stored(ScreenStates.Cheddar, "cheddar"));
        Context.Services.AddSingleton(core.GetRequiredService<FoodService>());
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
        ServingLabel = from.ServingLabel,
        ServingAmount = from.ServingAmount,
        SearchText = from.Name.ToLowerInvariant(),
    };

    private async Task<int> Insert(Food food)
    {
        await connection.InsertAsync(food);
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
            Assert.That(Text(cut, "screen-title"), Is.EqualTo("Margherita pizza"));
            Assert.That(cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("1"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 202 g"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("521 kcal"));
        });
    }

    [Test]
    public void FoodWithNoServing_OpensInGramsWithNoSwitch()
    {
        var cut = Render(cheddarId, Meal.Lunch);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-mode"), Is.False);
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("value"), Is.EqualTo("100"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("g"));
        });
    }

    [Test]
    public async Task FoodWithAServingOutOfRange_ShowsNoSwitch()
    {
        var id = await Insert(new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "long",
            Name = "Long label",
            Kcal = 100,
            ServingLabel = new string('x', FoodService.MaxServingLabelLength + 1),
            ServingAmount = 50,
            SearchText = "long label",
        });

        var cut = Render(id, Meal.Lunch);

        Assert.That(Exists(cut, "serving-mode"), Is.False);
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

        cut.Find("[data-testid=serving-mode-grams]").Change(true);
        var gramsShown = cut.Find("[data-testid=serving-amount]").GetAttribute("value");
        var kcalInGrams = Text(cut, "serving-adds-kcal");
        await cut.Find("[data-testid=serving-amount]").InputAsync("150");
        cut.Find("[data-testid=serving-mode-servings]").Change(true);
        var servingsShown = cut.Find("[data-testid=serving-amount]").GetAttribute("value");
        var weightInServings = Text(cut, "serving-weight");
        var kcalInServings = Text(cut, "serving-adds-kcal");
        cut.Find("[data-testid=serving-mode-grams]").Change(true);

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

        cut.Find("[data-testid=serving-mode-grams]").Change(true);
        var afterGrams = AddDisabled(cut);
        cut.Find("[data-testid=serving-mode-servings]").Change(true);

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
    public void UseServing_FromGrams_SwitchesToOneServing()
    {
        var cut = Render(pizzaId, Meal.Lunch);
        cut.Find("[data-testid=serving-mode-grams]").Change(true);

        cut.Find("[data-testid=serving-use-serving]").Click();

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

    [Test]
    public async Task ZeroAmount_Add_SavesNothing()
    {
        var added = false;
        var cut = Render(cheddarId, Meal.Lunch, () => added = true);
        await cut.Find("[data-testid=serving-amount]").InputAsync("0");

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        Assert.Multiple(async () =>
        {
            Assert.That(await Entries(), Is.Empty);
            Assert.That(added, Is.False);
        });
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
    public void ChoosingAnotherMeal_ChangesTheAddButton()
    {
        var cut = Render(cheddarId, Meal.Lunch);

        cut.Find("[data-testid=serving-meal-snacks]").Change(true);

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
        cut.Find("[data-testid=serving-mode-grams]").Change(true);
        await cut.Find("[data-testid=serving-amount]").InputAsync("150");

        await cut.Find("[data-testid=serving-add]").ClickAsync(new());

        var entry = (await Entries()).Single();
        Assert.That((entry.Amount, entry.Servings), Is.EqualTo((150.0, (double?)null)));
    }

    [Test]
    public async Task Add_AfterSwitchingBackToServings_SavesTheExactWeightAndTheRoundedCount()
    {
        var cut = Render(pizzaId, Meal.Lunch);
        cut.Find("[data-testid=serving-mode-grams]").Change(true);
        await cut.Find("[data-testid=serving-amount]").InputAsync("150");
        cut.Find("[data-testid=serving-mode-servings]").Change(true);

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
    public void Back_RaisesOnBack()
    {
        var back = false;
        var cut = Render(cheddarId, Meal.Lunch, more: p => p.Add(x => x.OnBack, () => back = true));

        cut.Find("[data-testid=back]").Click();

        Assert.That(back, Is.True);
    }
}
