using AngleSharp.Dom;
using CalorieTracker.Core.Data;
using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Models.Serving;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Stories;

/// <summary>A screen state: its stories section and story name, and how to render it.</summary>
/// <param name="Section">Stories title, e.g. "Screens/Settings".</param>
/// <param name="Name">Story name.</param>
/// <param name="Render">Renders the state with every callback set and returns its elements.</param>
public sealed record ScreenState(string Section, string Name, Func<BunitContext, IReadOnlyList<IElement>> Render)
{
    public override string ToString() => $"{Section}/{Name}";
}

/// <summary>Every screen state that has a story, in story order. Renders mirror the stories, with every callback set.</summary>
public static class ScreenStates
{
    private static readonly IReadOnlyList<Food> SampleFoods =
    [
        new() { Id = 1, Name = "Oats, rolled", Type = FoodType.Ingredient, Unit = FoodUnit.Gram, Kcal = 375, ProteinG = 11, CarbsG = 60, FatG = 9, FibreG = 8.5, SugarsG = 1, SaturatesG = 1.6, SaltG = 0 },
        new() { Id = 2, Name = "Porridge oats", Brand = "Quaker", Type = FoodType.Food, Unit = FoodUnit.Gram, Kcal = 372, ProteinG = 11, CarbsG = 60, FatG = 8.5 },
        new() { Id = 3, Name = "Oat drink", Brand = "Oatly", Type = FoodType.Drink, Unit = FoodUnit.Millilitre, Kcal = 46, ProteinG = 1, CarbsG = 6.7, FatG = 1.5 },
    ];

    internal static readonly Food Pizza = new()
    {
        Id = 11,
        Name = "Margherita pizza",
        Brand = "Casa Roma",
        Type = FoodType.Food,
        Unit = FoodUnit.Gram,
        Kcal = 258,
        ProteinG = 11.2,
        CarbsG = 31.5,
        FatG = 9.9,
        ServingLabel = "½ pizza (201.8 g)",
        ServingAmount = 201.8,
    };

    internal static readonly Food Biscuits = new()
    {
        Id = 12,
        Name = "Digestive biscuits",
        Brand = "Morning Mill",
        Type = FoodType.Food,
        Unit = FoodUnit.Gram,
        Kcal = 487,
        ProteinG = 7,
        CarbsG = 62,
        FatG = 22,
    };

    internal static readonly Food Cheddar = new()
    {
        Id = 13,
        Name = "Cheddar cheese",
        Type = FoodType.Ingredient,
        Unit = FoodUnit.Gram,
        Kcal = 416,
        ProteinG = 25.4,
        CarbsG = 0.1,
        FatG = 34.9,
    };

    internal static readonly Food Milk = new()
    {
        Id = 14,
        Name = "Semi-skimmed milk",
        Type = FoodType.Drink,
        Unit = FoodUnit.Millilitre,
        Kcal = 47,
        ProteinG = 3.5,
        CarbsG = 4.8,
        FatG = 1.7,
    };

    private static readonly IReadOnlyList<Food> Ingredients = SampleFoods.Where(f => f.Type == FoodType.Ingredient).ToList();

    public static IReadOnlyList<ScreenState> All { get; } =
    [
        new("Screens/Startup", "Loading", c => c.Render<StartupView>().FindAll("*")),
        new("Screens/Startup", "Load failed", c => c.Render<StartupView>(p => p.Add(x => x.Failed, true)).FindAll("*")),

        new("Screens/Dashboard", "Empty", c => c.Render<DashboardView>(p => p
            .Add(x => x.OnSettings, () => { })
            .Add(x => x.OnNavigate, (NavDestination _) => { })).FindAll("*")),

        new("Screens/Settings", "Default", c => Settings(c, version: "1.0.0", target: 2100)),
        new("Screens/Settings", "Dark chosen", c => Settings(c, version: "1.0.0", theme: ThemePreference.Dark, target: 2100)),
        new("Screens/Settings", "No version", c => Settings(c)),
        new("Screens/Settings", "Target set", c => Settings(c, version: "1.0.0", target: 2100)),
        new("Screens/Settings", "Target not set", c => Settings(c, version: "1.0.0")),
        new("Screens/Settings", "Invalid target", c => Settings(c, version: "1.0.0", target: 0, targetInvalidMessage: "Enter a whole number above 0, or clear the field.")),

        new("Screens/FoodSearch", "Loading", c => FoodSearch(c, query: "oat", filter: FoodType.Ingredient, loading: true)),
        new("Screens/FoodSearch", "Nothing typed", c => FoodSearch(c, filter: FoodType.Ingredient)),
        new("Screens/FoodSearch", "Results", c => FoodSearch(c, query: "oat", filter: FoodType.Ingredient, results: Ingredients)),
        new("Screens/FoodSearch", "Foods filter", c => FoodSearch(c, query: "oat", filter: FoodType.Food, results: SampleFoods.Where(f => f.Type == FoodType.Food).ToList())),
        new("Screens/FoodSearch", "Drinks filter", c => FoodSearch(c, query: "oat", filter: FoodType.Drink, results: SampleFoods.Where(f => f.Type == FoodType.Drink).ToList())),
        new("Screens/FoodSearch", "All filter", c => FoodSearch(c, query: "oat", results: SampleFoods)),
        new("Screens/FoodSearch", "No results", c => FoodSearch(c, query: "zzz", filter: FoodType.Ingredient)),
        new("Screens/FoodSearch", "No match on a filter", c => FoodSearch(c, query: "oat", filter: FoodType.Drink)),
        new("Screens/FoodSearch", "Food selected", c => FoodSearch(c, query: "oat", filter: FoodType.Ingredient, results: Ingredients, selected: Ingredients[0])),
        new("Screens/FoodSearch", "Search failed", c => FoodSearch(c, query: "oat", filter: FoodType.Ingredient, failed: true)),
        new("Screens/Scan", "Opening camera", c => Scan(c, ScanState.OpeningCamera)),
        new("Screens/Scan", "Closing camera", c => Scan(c, ScanState.Closing)),
        new("Screens/Scan", "Looking up", c => Scan(c, ScanState.LookingUp)),
        new("Screens/Scan", "Found", c => Scan(c, ScanState.Found, new("Porridge oats", "Morning Mill", 372, 11, 60, 8))),
        new("Screens/Scan", "Found liquid", c => Scan(c, ScanState.Found, new("Semi-skimmed milk", null, 47, 3.5m, 4.8m, 1.7m, IsLiquid: true))),
        new("Screens/Scan", "Found with extra nutrients", c => Scan(c, ScanState.Found, new("Porridge oats", "Morning Mill", 372, 11, 60, 8, Fibre: 9, Sugars: 1.1m, Saturates: 1.4m, Salt: 0.01m))),
        new("Screens/Scan", "Found with warning", c => Scan(c, ScanState.Found, new("Chocolate spread", "Nutty Co", 250, 6, 55, 31))),
        new("Screens/Scan", "Not found", c => Scan(c, ScanState.NotFound)),
        new("Screens/Scan", "Camera unavailable", c => Scan(c, ScanState.CameraUnavailable)),

        new("Screens/Serving", "Servings", c => Serving(c, Pizza, EntryMode.Servings, 1, 201.8, Meal.Lunch)),
        new("Screens/Serving", "Grams with a serving", c => Serving(c, Pizza, EntryMode.Grams, 150, 150, Meal.Lunch)),
        new("Screens/Serving", "Scanned product with no serving", c => Serving(c, Biscuits, EntryMode.Grams, 30, 30, Meal.Lunch)),
        new("Screens/Serving", "CoFID ingredient", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch)),
        new("Screens/Serving", "Liquid", c => Serving(c, Milk, EntryMode.Grams, 250, 250, Meal.Breakfast)),
        new("Screens/Serving", "Preset meal", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Dinner)),
        new("Screens/Serving", "Suggested meal", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, suggestedAt: new TimeOnly(12, 40))),
        new("Screens/Serving", "Invalid amount", c => Serving(c, Cheddar, EntryMode.Grams, 0, 0, Meal.Lunch)),
        new("Screens/Serving", "Amount too large", c => Serving(c, Cheddar, EntryMode.Grams, 12000, 12000, Meal.Lunch)),
        new("Screens/Serving", "Saving", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, saving: true)),
        new("Screens/Serving", "Save failed", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, saveFailed: true)),
        new("Screens/Serving", "Loading", c => Serving(c, null, loading: true)),
        new("Screens/Serving", "Load failed", c => Serving(c, null, loadFailed: true)),
        new("Screens/Serving", "Food not found", c => Serving(c, null)),

        new("Screens/Error", "Error", c => c.Render<ErrorView>(p => p.Add(x => x.OnRestart, () => { })).FindAll("*")),
    ];

    private static IReadOnlyList<IElement> FoodSearch(BunitContext context, string query = "", FoodType? filter = null, IReadOnlyList<Food>? results = null, Food? selected = null, bool loading = false, bool failed = false) =>
        context.Render<FoodSearchView>(p => p
            .Add(x => x.Query, query)
            .Add(x => x.Filter, filter)
            .Add(x => x.OnFilterChanged, (FoodType? _) => { })
            .Add(x => x.Results, results ?? [])
            .Add(x => x.Selected, selected)
            .Add(x => x.Loading, loading)
            .Add(x => x.Failed, failed)
            .Add(x => x.OnQueryChanged, (string _) => { })
            .Add(x => x.OnSelect, (Food _) => { })
            .Add(x => x.OnClearSelection, () => { })
            .Add(x => x.OnRetry, () => { })
            .Add(x => x.OnNavigate, (NavDestination _) => { }))
            .FindAll("*");

    internal static IReadOnlyList<IElement> Serving(BunitContext context, Food? food, EntryMode mode = EntryMode.Servings, decimal? amount = null, double? grams = null, Meal meal = Meal.Breakfast,
        TimeOnly? suggestedAt = null, bool saving = false, bool saveFailed = false, bool loading = false, bool loadFailed = false) =>
        context.Render<ServingView>(p => p
            .Add(x => x.Food, food)
            .Add(x => x.Mode, mode)
            .Add(x => x.Amount, amount)
            .Add(x => x.Grams, grams)
            .Add(x => x.Meal, meal)
            .Add(x => x.SuggestedAt, suggestedAt)
            .Add(x => x.Saving, saving)
            .Add(x => x.SaveFailed, saveFailed)
            .Add(x => x.Loading, loading)
            .Add(x => x.LoadFailed, loadFailed)
            .Add(x => x.OnModeChanged, (EntryMode _) => { })
            .Add(x => x.OnAmountChanged, (decimal? _) => { })
            .Add(x => x.OnUseServing, () => { })
            .Add(x => x.OnMealChanged, (Meal _) => { })
            .Add(x => x.OnAdd, () => { })
            .Add(x => x.OnRetry, () => { })
            .Add(x => x.OnBack, () => { }))
            .FindAll("*");

    private static IReadOnlyList<IElement> Scan(BunitContext context, ScanState state, ScannedProduct? product = null) =>
        context.Render<ScanView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.Product, product)
            .Add(x => x.OnScanAgain, () => { })
            .Add(x => x.OnNavigate, (NavDestination _) => { }))
            .FindAll("*");

    private static IReadOnlyList<IElement> Settings(BunitContext context, string? version = null, ThemePreference theme = ThemePreference.System, decimal? target = null, string? targetInvalidMessage = null) =>
        context.Render<SettingsView>(p => p
            .Add(x => x.Version, version)
            .Add(x => x.Theme, theme)
            .Add(x => x.Target, target)
            .Add(x => x.TargetInvalidMessage, targetInvalidMessage)
            .Add(x => x.OnTargetChanged, (decimal? _) => { })
            .Add(x => x.OnThemeChanged, (ThemePreference _) => { })
            .Add(x => x.OnBack, () => { }))
            .FindAll("*");
}
