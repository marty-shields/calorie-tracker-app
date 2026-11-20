using CalorieTracker.Core.Data;
using CalorieTracker.UI.Models.Serving;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Stories.Stories.Screens;
using CalorieTracker.UI.Tests.Stories;
using StoredServing = CalorieTracker.Core.Data.Serving;

namespace CalorieTracker.UI.Tests.Screens;

public class ServingViewTests : ComponentTestBase
{
    private IRenderedComponent<ServingView> Render(Food? food, EntryMode mode = EntryMode.Servings, decimal? amount = 1, double? grams = 201.8, Meal meal = Meal.Lunch,
        TimeOnly? suggestedAt = null, bool saving = false, bool saveFailed = false, Action<ComponentParameterCollectionBuilder<ServingView>>? more = null, IReadOnlyList<StoredServing>? servings = null) =>
        Context.Render<ServingView>(p =>
        {
            p.Add(x => x.Food, food).Add(x => x.Servings, servings ?? DefaultServings(food)).Add(x => x.Mode, mode).Add(x => x.Amount, amount).Add(x => x.Grams, grams).Add(x => x.Meal, meal)
                .Add(x => x.SuggestedAt, suggestedAt).Add(x => x.Saving, saving).Add(x => x.SaveFailed, saveFailed);
            more?.Invoke(p);
        });

    /// <summary>The servings the page would pass for the sample foods; for any other food, none.</summary>
    private static IReadOnlyList<StoredServing> DefaultServings(Food? food) => food switch
    {
        null => [],
        { Id: 11 } => ServingSamples.PizzaServings,
        _ => [],
    };

    private static string Text(IRenderedComponent<ServingView> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static bool Exists(IRenderedComponent<ServingView> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    [Test]
    public void Amount_UsesTheDecimalKeyboard_AndTheMinusIsOffAtZero()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-amount]").GetAttribute("inputmode"), Is.EqualTo("decimal"));
            Assert.That(cut.Find("[data-testid=serving-amount-minus]").HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public void Amount_MinusIsOnAboveZero() =>
        Assert.That(Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30).Find("[data-testid=serving-amount-minus]").HasAttribute("disabled"), Is.False);

    [Test]
    public void Title_IsTheFoodsName_AndPerLineGivesKcalAndMacros()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "screen-title"), Is.EqualTo("Cheddar cheese"));
            Assert.That(Text(cut, "serving-per"), Is.EqualTo("Per 100 g · 416 kcal · P 25.4 · C 0.1 · F 34.9"));
        });
    }

    [Test]
    public void PerLine_ForAFoodWithAServing_HasKcalOnly()
    {
        var cut = Render(ScreenStates.Pizza);

        Assert.That(Text(cut, "serving-per"), Is.EqualTo($"Per 100 g · {ScreenStates.Pizza.Kcal.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} kcal"));
    }

    [Test]
    public void PerLine_ForALiquid_IsPer100Ml()
    {
        var cut = Render(ScreenStates.Milk, EntryMode.Grams, 250, 250);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-per"), Does.StartWith("Per 100 ml · 47 kcal"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("ml"));
        });
    }

    [Test]
    public void MillilitreFood_WithAServing_ShowsMlOnTheAmountTheServingAndThePer100Labels()
    {
        var drink = new Food { Id = 15, Name = "Oat drink", Unit = FoodUnit.Millilitre, Kcal = 46, ProteinG = 1, CarbsG = 6.7, FatG = 1.5 };
        IReadOnlyList<StoredServing> glass = [new StoredServing { Id = 1, FoodId = drink.Id, Label = "1 glass", Amount = 200, Source = ServingSource.User }];
        var inMl = Render(drink, EntryMode.Grams, 250, 250, servings: glass);
        var inServings = Render(drink, EntryMode.Servings, 2, 400, servings: glass);

        Assert.Multiple(() =>
        {
            Assert.That(Text(inMl, "serving-per"), Does.StartWith("Per 100 ml"));
            Assert.That(Text(inMl, "serving-amount-unit"), Is.EqualTo("ml"));
            Assert.That(Text(inMl, "serving-adds-detail"), Does.StartWith("250 ml of"));
            Assert.That(Text(inServings, "serving-per"), Does.StartWith("Per 100 ml"));
            Assert.That(Text(inServings, "serving-weight"), Is.EqualTo("= 400 ml"));
            Assert.That(Text(inServings, "serving-adds-detail"), Does.StartWith("400 ml of"));
            Assert.That(inMl.Markup, Does.Not.Contain("100 g").And.Not.Contain("= 250 g"));
            Assert.That(inServings.Markup, Does.Not.Contain("100 g").And.Not.Contain("= 400 g"));
        });
    }

    [Test]
    public void Switch_FoodWithALabelAndAWeight_Shows_Grams_And_Servings_WithServingsChosen()
    {
        var cut = Render(ScreenStates.Pizza);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-mode"), Is.True);
            Assert.That(cut.FindAll("[data-testid=serving-mode] .segment").Select(s => s.TextContent.Trim()), Is.EqualTo(new[] { "Grams", "Servings" }));
            Assert.That(cut.Find("[data-testid=serving-mode-servings]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=serving-mode-grams]").HasAttribute("checked"), Is.False);
        });
    }

    [Test]
    public void Switch_FoodWithNoServing_IsHiddenAndOnlyGramsShow()
    {
        var cut = Render(ScreenStates.Biscuits, EntryMode.Servings, 30, 30);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-mode"), Is.False);
            Assert.That(Exists(cut, "serving-label"), Is.False);
            Assert.That(Exists(cut, "serving-use-serving"), Is.False);
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("g"));
        });
    }

    [Test]
    public void Switch_FoodWithNoServings_IsHidden()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 50, 50);

        Assert.That(Exists(cut, "serving-mode"), Is.False);
    }

    [Test]
    public void Switch_FoodWithOneServing_Shows() =>
        Assert.That(Exists(Render(ScreenStates.Pizza), "serving-mode"), Is.True);

    [Test]
    public void ServingsMode_ShowsTheLabelCard_HowMany_ServingUnit_AndTheWeight()
    {
        var cut = Render(ScreenStates.Pizza);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("h2").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Enter as", "How many", "Your servings", "Meal", "This adds" }));
            Assert.That(Text(cut, "serving-label"), Is.EqualTo("½ pizza (201.8 g)"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("serving"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 202 g"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("521 kcal"));
            Assert.That(Text(cut, "serving-adds-detail"), Is.EqualTo("202 g of Casa Roma Margherita pizza"));
            Assert.That(Exists(cut, "serving-use-serving"), Is.False);
        });
    }

    [Test]
    public void GramsMode_OfAFoodWithAServing_ShowsAmount_UseServing_AndNoLabelCard()
    {
        var cut = Render(ScreenStates.Pizza, EntryMode.Grams, 150, 150);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("h2").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Enter as", "Amount", "Your servings", "Meal", "This adds" }));
            Assert.That(Text(cut, "serving-use-serving"), Is.EqualTo("Use serving: ½ pizza (201.8 g)"));
            Assert.That(Exists(cut, "serving-label"), Is.False);
            Assert.That(Exists(cut, "serving-weight"), Is.False);
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("g"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("387 kcal"));
            Assert.That(Text(cut, "serving-adds-detail"), Is.EqualTo("150 g of Casa Roma Margherita pizza"));
        });
    }

    [Test]
    public void GramsOnlyFood_ThisAddsShowsTheMacrosForTheAmount()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("125 kcal"));
            Assert.That(Text(cut, "serving-adds-detail"), Is.EqualTo("Protein 7.6 g · Carbs 0 g · Fat 10.5 g"));
            Assert.That(cut.FindAll("h2").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Amount", "Your servings", "Meal", "This adds" }));
        });
    }

    [Test]
    public void Meal_ShowsTheFourChipsInOrderWithThePresetSelected_AndNamesItOnAdd()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, Meal.Dinner);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=serving-meal] .chip").Select(c => c.TextContent.Trim()), Is.EqualTo(new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }));
            Assert.That(cut.Find("[data-testid=serving-meal-dinner]").HasAttribute("checked"), Is.True);
            Assert.That(cut.FindAll("[data-testid=serving-meal] input[checked]"), Has.Count.EqualTo(1));
            Assert.That(Text(cut, "serving-add"), Is.EqualTo("Add to Dinner"));
        });
    }

    [Test]
    public void Suggestion_ShowsOnlyWhenATimeIsGiven()
    {
        var suggested = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, suggestedAt: new TimeOnly(12, 40));
        var preset = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30);

        Assert.Multiple(() =>
        {
            Assert.That(Text(suggested, "serving-suggestion"), Is.EqualTo("Suggested from the time: 12:40"));
            Assert.That(Exists(preset, "serving-suggestion"), Is.False);
        });
    }

    [TestCase(0.0, "Enter an amount above 0.")]
    [TestCase(-5.0, "Enter an amount above 0.")]
    [TestCase(null, "Enter an amount above 0.")]
    [TestCase(10_000.5, "Enter an amount of 10,000 g or less.")]
    public void InvalidAmount_ShowsTheMessage_DisablesAdd_AndHidesThisAdds(double? grams, string message)
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, (decimal?)grams, grams);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-amount-error"), Is.EqualTo(message));
            Assert.That(cut.Find("[data-testid=serving-add]").HasAttribute("disabled"), Is.True);
            Assert.That(Exists(cut, "serving-adds"), Is.False);
            Assert.That(Text(cut, "serving-add"), Is.EqualTo("Add to Lunch"));
        });
    }

    [Test]
    public void AmountAtTheLimit_IsValid()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 10_000, 10_000);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-amount-error"), Is.False);
            Assert.That(cut.Find("[data-testid=serving-add]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public void ServingsOverTheLimit_SaysSo()
    {
        var cut = Render(ScreenStates.Pizza, EntryMode.Servings, 100, 20_180);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-amount-error"), Is.EqualTo("That is over 10,000 g. Enter fewer servings."));
            Assert.That(cut.Find("[data-testid=serving-add]").HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public void Saving_ShowsAddBusy()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, saving: true);

        var add = cut.Find("[data-testid=serving-add]");
        Assert.Multiple(() =>
        {
            Assert.That(add.GetAttribute("aria-busy"), Is.EqualTo("true"));
            Assert.That(add.TextContent.Trim(), Is.EqualTo("Adding…"));
        });
    }

    [Test]
    public void SaveFailed_ShowsTheError_AndKeepsTheFormAndAddEnabled()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, saveFailed: true);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-error"), Is.EqualTo("Couldn't add this food. Try again."));
            Assert.That(Exists(cut, "serving-amount"), Is.True);
            Assert.That(cut.Find("[data-testid=serving-add]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public void StatusMessages_CarryTheStatusErrorClass()
    {
        var save = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, saveFailed: true);
        var remove = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.Editing, true).Add(x => x.RemoveFailed, true));
        var gone = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.Editing, true).Add(x => x.EntryGone, true));

        Assert.Multiple(() =>
        {
            Assert.That(save.Find("[data-testid=serving-error]").ClassList, Does.Contain("status-error"));
            Assert.That(remove.Find("[data-testid=serving-error]").ClassList, Does.Contain("status-error"));
            Assert.That(gone.Find("[data-testid=serving-gone]").ClassList, Does.Contain("status-error"));
        });
    }

    [Test]
    public void NoSaveError_ShowsNoMessage() =>
        Assert.That(Exists(Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30), "serving-error"), Is.False);

    [Test]
    public void LoadingFailedAndNotFound_ShowTheirOwnStateWithoutAdd()
    {
        var loading = Render(null, more: p => p.Add(x => x.Loading, true));
        var failed = Render(null, more: p => p.Add(x => x.LoadFailed, true));
        var missing = Render(null);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(loading, "serving-loading"), Is.True);
            Assert.That(Text(failed, "serving-load-failed-error"), Is.EqualTo("Couldn't load this food"));
            Assert.That(Text(missing, "serving-not-found-message"), Is.EqualTo("Food not found"));
            Assert.That(new[] { loading, failed, missing }.Select(c => Exists(c, "serving-add")), Is.All.False);
        });
    }

    [Test]
    public void Callbacks_AreRaised()
    {
        var log = new List<string>();
        var cut = Render(ScreenStates.Pizza, EntryMode.Grams, 150, 150, more: p => p
            .Add(x => x.OnModeChanged, m => log.Add($"mode {m}"))
            .Add(x => x.OnAmountChanged, a => log.Add($"amount {a}"))
            .Add(x => x.OnUseServing, () => log.Add("use"))
            .Add(x => x.OnMealChanged, m => log.Add($"meal {m}"))
            .Add(x => x.OnAdd, () => log.Add("add"))
            .Add(x => x.OnBack, () => log.Add("back")));

        cut.Find("[data-testid=serving-mode-servings]").Change(true);
        cut.Find("[data-testid=serving-amount-plus]").Click();
        cut.Find("[data-testid=serving-use-serving]").Click();
        cut.Find("[data-testid=serving-meal-snacks]").Change(true);
        cut.Find("[data-testid=serving-add]").Click();
        cut.Find("[data-testid=back]").Click();

        Assert.That(log, Is.EqualTo(new[] { "mode Servings", "amount 160", "use", "meal Snacks", "add", "back" }));
    }

    [Test]
    public void Stepper_MovesByTenGramsOrOneServing()
    {
        var amounts = new List<decimal?>();
        var grams = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.OnAmountChanged, amounts.Add));
        var servings = Render(ScreenStates.Pizza, EntryMode.Servings, 1, 201.8, more: p => p.Add(x => x.OnAmountChanged, amounts.Add));

        grams.Find("[data-testid=serving-amount-plus]").Click();
        servings.Find("[data-testid=serving-amount-plus]").Click();

        Assert.That(amounts, Is.EqualTo(new decimal?[] { 40, 2 }));
    }

    [Test]
    public void EditMode_ShowsTheTrashIcon_TheDayAndMeal_SaveAndThisEntry()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.Editing, true).Add(x => x.Date, new DateOnly(2031, 7, 15)));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=serving-remove]").GetAttribute("aria-label"), Is.EqualTo("Remove entry"));
            Assert.That(Text(cut, "serving-when"), Is.EqualTo("Lunch · 15 Jul"));
            Assert.That(Text(cut, "serving-save"), Is.EqualTo("Save"));
            Assert.That(Exists(cut, "serving-add"), Is.False);
            Assert.That(Exists(cut, "serving-per"), Is.False);
            Assert.That(Exists(cut, "serving-suggestion"), Is.False);
            Assert.That(cut.Find("[data-testid=serving-adds] h2").TextContent, Is.EqualTo("This entry"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("125 kcal"));
        });
    }

    [Test]
    public void AddMode_HasNoTrashIconAndNoSave()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-remove"), Is.False);
            Assert.That(Exists(cut, "serving-save"), Is.False);
            Assert.That(Exists(cut, "serving-when"), Is.False);
        });
    }

    [Test]
    public async Task EditMode_SaveAndTrash_RaiseTheirCallbacks()
    {
        var log = new List<string>();
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.Editing, true)
            .Add(x => x.OnSave, () => log.Add("save")).Add(x => x.OnRemove, () => log.Add("remove")).Add(x => x.OnAdd, () => log.Add("add")));

        await cut.Find("[data-testid=serving-save]").ClickAsync(new());
        await cut.Find("[data-testid=serving-remove]").ClickAsync(new());

        Assert.That(log, Is.EqualTo(new[] { "save", "remove" }));
    }

    [Test]
    public void EditMode_SaveIsOffForAnInvalidAmount_AndWhenTheEntryIsGone()
    {
        var invalid = Render(ScreenStates.Cheddar, EntryMode.Grams, 0, 0, more: p => p.Add(x => x.Editing, true));
        var gone = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.Editing, true).Add(x => x.EntryGone, true));

        Assert.Multiple(() =>
        {
            Assert.That(invalid.Find("[data-testid=serving-save]").HasAttribute("disabled"), Is.True);
            Assert.That(gone.Find("[data-testid=serving-save]").HasAttribute("disabled"), Is.True);
            Assert.That(Text(gone, "serving-gone"), Is.EqualTo("This entry was already removed."));
        });
    }

    [Test]
    public void EditMode_SaveAndRemoveErrors_SaySoInEditWords()
    {
        var save = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, saveFailed: true, more: p => p.Add(x => x.Editing, true));
        var remove = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.Editing, true).Add(x => x.RemoveFailed, true));

        Assert.Multiple(() =>
        {
            Assert.That(Text(save, "serving-error"), Is.EqualTo("Couldn't save this entry. Try again."));
            Assert.That(Text(remove, "serving-error"), Is.EqualTo("Couldn't remove this entry. Try again."));
        });
    }

    [Test]
    public void EditMode_Saving_ShowsSaveBusy()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, saving: true, more: p => p.Add(x => x.Editing, true));

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-save"), Is.EqualTo("Saving…"));
            Assert.That(cut.Find("[data-testid=serving-save]").HasAttribute("aria-busy"), Is.True);
        });
    }

    private IRenderedComponent<ServingView> RenderCola(IReadOnlyList<StoredServing> servings, int? chosen, EntryMode mode = EntryMode.Servings, decimal? amount = 1, double? grams = 330, Action<ComponentParameterCollectionBuilder<ServingView>>? more = null) =>
        Render(ServingSamples.Cola, mode, amount, grams, servings: servings, more: p =>
        {
            p.Add(x => x.ServingId, chosen);
            more?.Invoke(p);
        });

    [Test]
    public void TwoServings_ShowTheListInOrder_WithTheChosenOneSelected_AndNoLabelCard()
    {
        var cut = RenderCola(ServingSamples.ColaServings, 161, grams: 250);

        var options = cut.FindAll("[data-testid=serving-select] option");
        Assert.Multiple(() =>
        {
            Assert.That(options.Select(o => o.TextContent.Trim()), Is.EqualTo(new[] { "My mug", "1 glass (250 ml)" }));
            Assert.That(options.Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { false, true }));
            Assert.That(Exists(cut, "serving-label"), Is.False);
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 250 ml"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("serving"));
        });
    }

    [Test]
    public void TwoServings_WithNoChosenId_SelectTheFirst()
    {
        var cut = RenderCola(ServingSamples.ColaServings, null, grams: 200);

        Assert.That(cut.FindAll("[data-testid=serving-select] option").Select(o => o.HasAttribute("selected")), Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public void OneServing_KeepsTodaysLookWithTheLabelCardAndNoList()
    {
        var cut = Render(ScreenStates.Pizza);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-select"), Is.False);
            Assert.That(Text(cut, "serving-label"), Is.EqualTo("½ pizza (201.8 g)"));
        });
    }

    [Test]
    public void PickingAServing_RaisesItsId()
    {
        var picked = new List<int>();
        var cut = RenderCola(ServingSamples.ColaServings, 161, grams: 250, more: p => p.Add(x => x.OnServingChanged, picked.Add));

        cut.Find("[data-testid=serving-select]").Change("0");

        Assert.That(picked, Is.EqualTo(new[] { 163 }));
    }

    [Test]
    public void GramsMode_UseServingNamesTheChosenServing()
    {
        var cut = RenderCola(ServingSamples.ColaServings, 161, EntryMode.Grams, 100, 100);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "serving-use-serving"), Is.EqualTo("Use serving: 1 glass (250 ml)"));
            Assert.That(Exists(cut, "serving-select"), Is.False);
        });
    }

    [Test]
    public void YourServings_ShowsAddOwnWhenNotAdding_AndTheFormWhenAdding()
    {
        var idle = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30);
        var adding = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, "1 slice").Add(x => x.OwnAmount, 28));

        Assert.Multiple(() =>
        {
            Assert.That(Text(idle, "serving-own-add"), Is.EqualTo("Add your own serving"));
            Assert.That(Exists(idle, "serving-own-label"), Is.False);
            Assert.That(Exists(adding, "serving-own-add"), Is.False);
            Assert.That(adding.Find("[data-testid=serving-own-label]").GetAttribute("value"), Is.EqualTo("1 slice"));
            Assert.That(adding.Find("[data-testid=serving-own-amount]").GetAttribute("value"), Is.EqualTo("28"));
            Assert.That(Text(adding, "serving-own-amount-unit"), Is.EqualTo("g"));
            Assert.That(adding.Find("[data-testid=serving-own-save]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public void OwnForm_OfAMillilitreFood_ShowsMl()
    {
        var cut = RenderCola(ServingSamples.ColaServings, 161, more: p => p.Add(x => x.AddingOwn, true));

        Assert.That(Text(cut, "serving-own-amount-unit"), Is.EqualTo("ml"));
    }

    [TestCase(null, 28.0)]
    [TestCase("", 28.0)]
    [TestCase("   ", 28.0)]
    [TestCase("1 slice", null)]
    [TestCase("1 slice", 0.0)]
    [TestCase("1 slice", -2.0)]
    [TestCase("1 slice", 10_000.5)]
    public void OwnForm_SaveIsOffUntilTheNameAndSizeAreValid(string? label, double? amount)
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, label).Add(x => x.OwnAmount, (decimal?)amount));

        Assert.That(cut.Find("[data-testid=serving-own-save]").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void OwnForm_SaveIsOnForTheLimits()
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, new string('x', 60)).Add(x => x.OwnAmount, 10_000));

        Assert.That(cut.Find("[data-testid=serving-own-save]").HasAttribute("disabled"), Is.False);
    }

    [TestCase(0.0, "Enter an amount above 0.")]
    [TestCase(-1.0, "Enter an amount above 0.")]
    [TestCase(10_001.0, "Enter an amount of 10,000 g or less.")]
    public void OwnForm_AnInvalidSize_SaysSoUnderTheField(double amount, string message)
    {
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, "1 slice").Add(x => x.OwnAmount, (decimal)amount));

        Assert.That(Text(cut, "serving-own-amount-error"), Is.EqualTo(message));
    }

    [Test]
    public void OwnForm_ANameOverTheLimit_SaysSo_AndAnEmptyFormShowsNoError()
    {
        var long_ = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, new string('x', 61)).Add(x => x.OwnAmount, 5));
        var empty = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, "").Add(x => x.OwnAmount, (decimal?)null));

        Assert.Multiple(() =>
        {
            Assert.That(Text(long_, "serving-own-label-error"), Is.EqualTo("Use 60 characters or fewer."));
            Assert.That(Exists(empty, "serving-own-label-error"), Is.False);
            Assert.That(Exists(empty, "serving-own-amount-error"), Is.False);
        });
    }

    [Test]
    public void OwnForm_ShowsTheSaveError_AndSavingShowsBusy()
    {
        var failed = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, "1 slice").Add(x => x.OwnAmount, 28).Add(x => x.OwnError, "You already have a serving of that size."));
        var saving = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p.Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, "1 slice").Add(x => x.OwnAmount, 28).Add(x => x.SavingOwn, true));

        Assert.Multiple(() =>
        {
            Assert.That(Text(failed, "serving-own-error"), Is.EqualTo("You already have a serving of that size."));
            Assert.That(Text(saving, "serving-own-save"), Is.EqualTo("Saving…"));
            Assert.That(saving.Find("[data-testid=serving-own-cancel]").HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public void OwnForm_RaisesItsCallbacks()
    {
        var log = new List<string>();
        var cut = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p
            .Add(x => x.OnOwnOpen, () => log.Add("open"))
            .Add(x => x.OnOwnLabelChanged, t => log.Add($"label {t}"))
            .Add(x => x.OnOwnAmountChanged, a => log.Add($"amount {a}"))
            .Add(x => x.OnOwnSave, () => log.Add("save"))
            .Add(x => x.OnOwnCancel, () => log.Add("cancel")));
        cut.Find("[data-testid=serving-own-add]").Click();
        var adding = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30, more: p => p
            .Add(x => x.AddingOwn, true).Add(x => x.OwnLabel, "1 slice").Add(x => x.OwnAmount, 28)
            .Add(x => x.OnOwnLabelChanged, t => log.Add($"label {t}"))
            .Add(x => x.OnOwnAmountChanged, a => log.Add($"amount {a}"))
            .Add(x => x.OnOwnSave, () => log.Add("save"))
            .Add(x => x.OnOwnCancel, () => log.Add("cancel")));

        adding.Find("[data-testid=serving-own-label]").Input("A slice");
        adding.Find("[data-testid=serving-own-amount]").Input("30");
        adding.Find("[data-testid=serving-own-save]").Click();
        adding.Find("[data-testid=serving-own-cancel]").Click();

        Assert.That(log, Is.EqualTo(new[] { "open", "label A slice", "amount 30", "save", "cancel" }));
    }

    [Test]
    public void DeleteServing_ShowsWhileAServingIsChosen_NotForAFoodWithNone_AndRaisesItsCallback()
    {
        var deleted = 0;
        var chosen = RenderCola(ServingSamples.ColaServings, 163, grams: 200, more: p => p.Add(x => x.OnDeleteOwn, () => deleted++));
        var none = Render(ScreenStates.Cheddar, EntryMode.Grams, 30, 30);

        chosen.Find("[data-testid=serving-own-delete]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(Text(chosen, "serving-own-delete"), Is.EqualTo("Delete serving: My mug"));
            Assert.That(Exists(none, "serving-own-delete"), Is.False);
            Assert.That(deleted, Is.EqualTo(1));
        });
    }

    [Test]
    public void OwnServings_AreAlsoOfferedInEditMode()
    {
        var cut = RenderCola(ServingSamples.ColaServings, 163, grams: 200, more: p => p.Add(x => x.Editing, true));

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "serving-select"), Is.True);
            Assert.That(Exists(cut, "serving-own-add"), Is.True);
        });
    }
}
