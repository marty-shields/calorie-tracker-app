using CalorieTracker.Core.Data;
using CalorieTracker.UI.Models.Serving;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Tests.Stories;

namespace CalorieTracker.UI.Tests.Screens;

public class ServingViewTests : ComponentTestBase
{
    private IRenderedComponent<ServingView> Render(Food? food, EntryMode mode = EntryMode.Servings, decimal? amount = 1, double? grams = 201.8, Meal meal = Meal.Lunch,
        TimeOnly? suggestedAt = null, bool saving = false, bool saveFailed = false, Action<ComponentParameterCollectionBuilder<ServingView>>? more = null) =>
        Context.Render<ServingView>(p =>
        {
            p.Add(x => x.Food, food).Add(x => x.Mode, mode).Add(x => x.Amount, amount).Add(x => x.Grams, grams).Add(x => x.Meal, meal)
                .Add(x => x.SuggestedAt, suggestedAt).Add(x => x.Saving, saving).Add(x => x.SaveFailed, saveFailed);
            more?.Invoke(p);
        });

    private static string Text(IRenderedComponent<ServingView> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static bool Exists(IRenderedComponent<ServingView> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

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

    private static Food WithServing(string? label, double? weight) => new()
    {
        Id = 20,
        Name = "Pot",
        Unit = FoodUnit.Gram,
        Kcal = 100,
        ProteinG = 1,
        CarbsG = 1,
        FatG = 1,
        ServingLabel = label,
        ServingAmount = weight,
    };

    [TestCase(null, 100.0)]
    [TestCase("", 100.0)]
    [TestCase("   ", 100.0)]
    [TestCase("1 pot", null)]
    [TestCase("1 pot", 0.0)]
    [TestCase("1 pot", -5.0)]
    [TestCase("1 pot", 10_001.0)]
    public void Switch_WithoutBothALabelAndAWeightAboveZero_IsHidden(string? label, double? weight)
    {
        var cut = Render(WithServing(label, weight), EntryMode.Grams, 50, 50);

        Assert.That(Exists(cut, "serving-mode"), Is.False);
    }

    [Test]
    public void Switch_WithALabelAndAWeightAboveZero_Shows() =>
        Assert.That(Exists(Render(WithServing("1 pot", 0.5)), "serving-mode"), Is.True);

    [Test]
    public void ServingsMode_ShowsTheLabelCard_HowMany_ServingUnit_AndTheWeight()
    {
        var cut = Render(ScreenStates.Pizza);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("h2").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Enter as", "How many", "Meal", "This adds" }));
            Assert.That(Text(cut, "serving-label"), Is.EqualTo("½ pizza (201.8 g)"));
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("serving"));
            Assert.That(Text(cut, "serving-weight"), Is.EqualTo("= 202 g"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("521 kcal"));
            Assert.That(Text(cut, "serving-adds-detail"), Is.EqualTo("202 g of Margherita pizza"));
            Assert.That(Exists(cut, "serving-use-serving"), Is.False);
        });
    }

    [Test]
    public void GramsMode_OfAFoodWithAServing_ShowsAmount_UseServing_AndNoLabelCard()
    {
        var cut = Render(ScreenStates.Pizza, EntryMode.Grams, 150, 150);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("h2").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Enter as", "Amount", "Meal", "This adds" }));
            Assert.That(Text(cut, "serving-use-serving"), Is.EqualTo("Use serving: ½ pizza (201.8 g)"));
            Assert.That(Exists(cut, "serving-label"), Is.False);
            Assert.That(Exists(cut, "serving-weight"), Is.False);
            Assert.That(Text(cut, "serving-amount-unit"), Is.EqualTo("g"));
            Assert.That(Text(cut, "serving-adds-kcal"), Is.EqualTo("387 kcal"));
            Assert.That(Text(cut, "serving-adds-detail"), Is.EqualTo("150 g of Margherita pizza"));
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
            Assert.That(cut.FindAll("h2").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Amount", "Meal", "This adds" }));
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
}
