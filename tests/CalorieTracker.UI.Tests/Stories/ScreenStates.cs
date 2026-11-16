using AngleSharp.Dom;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Summary;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Models.Serving;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Stories.Stories.Screens;
using Microsoft.Extensions.DependencyInjection;

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
        new() { Id = 1, Name = "Oats, rolled", Unit = FoodUnit.Gram, Kcal = 375, ProteinG = 11, CarbsG = 60, FatG = 9, FibreG = 8.5, SugarsG = 1, SaturatesG = 1.6, SaltG = 0 },
        new() { Id = 2, Name = "Porridge oats", Brand = "Quaker", Unit = FoodUnit.Gram, Kcal = 372, ProteinG = 11, CarbsG = 60, FatG = 8.5 },
        new() { Id = 3, Name = "Oat drink", Brand = "Oatly", Unit = FoodUnit.Millilitre, Kcal = 46, ProteinG = 1, CarbsG = 6.7, FatG = 1.5 },
    ];

    internal static readonly Food Pizza = new()
    {
        Id = 11,
        Name = "Margherita pizza",
        Brand = "Casa Roma",
        Unit = FoodUnit.Gram,
        Kcal = 258,
        ProteinG = 11.2,
        CarbsG = 31.5,
        FatG = 9.9,
        ServingLabel = "½ pizza (201.8 g)",
        ServingAmount = 201.8,
    };

    internal static readonly Food Mars = ServingSamples.Mars;

    internal static readonly Food Biscuits = new()
    {
        Id = 12,
        Name = "Digestive biscuits",
        Brand = "Morning Mill",
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
        Unit = FoodUnit.Millilitre,
        Kcal = 47,
        ProteinG = 3.5,
        CarbsG = 4.8,
        FatG = 1.7,
    };

    public static IReadOnlyList<ScreenState> All { get; } =
    [
        new("Screens/Startup", "Loading", c => c.Render<StartupView>().FindAll("*")),
        new("Screens/Startup", "Load failed", c => c.Render<StartupView>(p => p.Add(x => x.Failed, true)).FindAll("*")),

        new("Screens/Dashboard", "First run", c => Dashboard(c, DashboardSamples.FirstRun)),
        new("Screens/Dashboard", "No target", c => Dashboard(c, DashboardSamples.NoTarget)),
        new("Screens/Dashboard", "Loaded", c => Dashboard(c, DashboardSamples.Loaded)),
        new("Screens/Dashboard", "Over target", c => Dashboard(c, DashboardSamples.OverTarget)),
        new("Screens/Dashboard", "Loading", c => Dashboard(c, null, loading: true)),
        new("Screens/Dashboard", "Load failed", c => Dashboard(c, null, loadFailed: true)),
        new("Screens/Dashboard", "Future day", c => Dashboard(c, DashboardSamples.Loaded, DashboardSamples.Today.AddDays(1))),
        new("Screens/Dashboard", "Branded foods", c => Dashboard(c, DashboardSamples.Branded)),
        new("Screens/Dashboard", "Entry removed", c => Dashboard(c, DashboardSamples.Loaded, snackbar: "Cheddar cheese removed")),

        new("Screens/Settings", "Default", c => Settings(c, version: "1.0.0", target: 2100)),
        new("Screens/Settings", "Dark chosen", c => Settings(c, version: "1.0.0", theme: ThemePreference.Dark, target: 2100)),
        new("Screens/Settings", "No version", c => Settings(c)),
        new("Screens/Settings", "Target set", c => Settings(c, version: "1.0.0", target: 2100)),
        new("Screens/Settings", "Target not set", c => Settings(c, version: "1.0.0")),
        new("Screens/Settings", "Invalid target", c => Settings(c, version: "1.0.0", target: 0, targetInvalidMessage: "Enter a whole number above 0, or clear the field.")),

        new("Screens/FoodSearch", "Loading", c => FoodSearch(c, query: "oat", loading: true)),
        new("Screens/FoodSearch", "Nothing typed", c => FoodSearch(c)),
        new("Screens/FoodSearch", "Nothing typed with history", c => FoodSearch(c, groups: FoodSearchSamples.IdleRecent)),
        new("Screens/FoodSearch", "Results", c => FoodSearch(c, query: "oat", groups: FoodSearchSamples.AllGroups)),
        new("Screens/FoodSearch", "Results with branded foods", c => FoodSearch(c, query: "mars", groups: FoodSearchSamples.BrandedGroup)),
        new("Screens/FoodSearch", "Results in one group", c => FoodSearch(c, query: "rolled", groups: FoodSearchSamples.OneGroup)),
        new("Screens/FoodSearch", "Results with more to show", c => FoodSearch(c, query: "oat", groups: FoodSearchSamples.LongGroup)),
        new("Screens/FoodSearch", "Results with an expanded group", c => FoodSearch(c, query: "oat", groups: FoodSearchSamples.LongGroup, expanded: [FoodGroupKind.Common])),
        new("Screens/FoodSearch", "No results", c => FoodSearch(c, query: "zzz")),
        new("Screens/FoodSearch", "Food selected", c => FoodSearch(c, query: "oat", groups: FoodSearchSamples.AllGroups, selected: SampleFoods[0])),
        new("Screens/FoodSearch", "Branded food selected", c => FoodSearch(c, query: "nutella", groups: FoodSearchSamples.BrandedGroup, selected: FoodSearchSamples.BrandedFoods[1])),
        new("Screens/FoodSearch", "Add to a meal", c => FoodSearch(c, query: "oat", groups: FoodSearchSamples.AllGroups, meal: Meal.Lunch)),
        new("Screens/FoodSearch", "Add to a meal with nothing typed", c => FoodSearch(c, meal: Meal.Snacks)),
        new("Screens/FoodSearch", "Add to a meal with history", c => FoodSearch(c, groups: FoodSearchSamples.Idle, meal: Meal.Breakfast)),
        new("Screens/FoodSearch", "Add to a meal with no results", c => FoodSearch(c, query: "xyzzy", meal: Meal.Lunch)),
        new("Screens/FoodSearch", "Search failed", c => FoodSearch(c, query: "oat", failed: true)),
        new("Screens/Scan", "Opening camera", c => Scan(c, ScanState.OpeningCamera)),
        new("Screens/Scan", "Closing camera", c => Scan(c, ScanState.Closing)),
        new("Screens/Scan", "Looking up", c => Scan(c, ScanState.LookingUp)),
        new("Screens/Scan", "Found", c => Scan(c, ScanState.Found, new("Porridge oats", "Morning Mill", 372, 11, 60, 8, FoodId: 1))),
        new("Screens/Scan", "Found liquid", c => Scan(c, ScanState.Found, new("Semi-skimmed milk", null, 47, 3.5m, 4.8m, 1.7m, IsLiquid: true, FoodId: 2))),
        new("Screens/Scan", "Found with extra nutrients", c => Scan(c, ScanState.Found, new("Porridge oats", "Morning Mill", 372, 11, 60, 8, Fibre: 9, Sugars: 1.1m, Saturates: 1.4m, Salt: 0.01m, FoodId: 1))),
        new("Screens/Scan", "Found with a serving", c => Scan(c, ScanState.Found, new("Digestive biscuits", "Morning Mill", 480, 7, 62, 22, FoodId: 3, ServingLabel: "1 biscuit", ServingAmount: 15))),
        new("Screens/Scan", "Found with brand and pack", c => Scan(c, ScanState.Found, new("Nutella", "Ferrero", 539, 6.3m, 57.5m, 30.9m, FoodId: 5, Brands: "Ferrero, Nutella", GenericName: "Hazelnut spread with cocoa", PackageLabel: "400 g e"))),
        new("Screens/Scan", "Found with warning", c => Scan(c, ScanState.Found, new("Chocolate spread", "Nutty Co", 250, 6, 55, 31, FoodId: 4))),
        new("Screens/Scan", "Not found", c => Scan(c, ScanState.NotFound)),
        new("Screens/Scan", "Camera unavailable", c => Scan(c, ScanState.CameraUnavailable)),

        new("Screens/Serving", "Servings", c => Serving(c, Pizza, EntryMode.Servings, 1, 201.8, Meal.Lunch)),
        new("Screens/Serving", "Grams with a serving", c => Serving(c, Pizza, EntryMode.Grams, 150, 150, Meal.Lunch)),
        new("Screens/Serving", "Pack as one serving", c => Serving(c, Mars, EntryMode.Servings, 1, 51, Meal.Snacks)),
        new("Screens/Serving", "Scanned product with no serving", c => Serving(c, Biscuits, EntryMode.Grams, 30, 30, Meal.Lunch)),
        new("Screens/Serving", "CoFID ingredient", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch)),
        new("Screens/Serving", "Liquid", c => Serving(c, Milk, EntryMode.Grams, 250, 250, Meal.Breakfast)),
        new("Screens/Serving", "Preset meal", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Dinner)),
        new("Screens/Serving", "Suggested meal", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, suggestedAt: new TimeOnly(12, 40))),
        new("Screens/Serving", "Invalid amount", c => Serving(c, Cheddar, EntryMode.Grams, 0, 0, Meal.Lunch)),
        new("Screens/Serving", "Amount too large", c => Serving(c, Cheddar, EntryMode.Grams, 12000, 12000, Meal.Lunch)),
        new("Screens/Serving", "Saving", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, saving: true)),
        new("Screens/Serving", "Save failed", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, saveFailed: true)),
        new("Screens/Serving", "Edit entry", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, editing: true)),
        new("Screens/Serving", "Edit entry in servings", c => Serving(c, Pizza, EntryMode.Servings, 1, 201.8, Meal.Lunch, editing: true)),
        new("Screens/Serving", "Saving entry", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, saving: true, editing: true)),
        new("Screens/Serving", "Entry save failed", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, saveFailed: true, editing: true)),
        new("Screens/Serving", "Entry remove failed", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, removeFailed: true, editing: true)),
        new("Screens/Serving", "Entry already removed", c => Serving(c, Cheddar, EntryMode.Grams, 30, 30, Meal.Lunch, entryGone: true, editing: true)),
        new("Screens/Serving", "Loading", c => Serving(c, null, loading: true)),
        new("Screens/Serving", "Load failed", c => Serving(c, null, loadFailed: true)),
        new("Screens/Serving", "Food not found", c => Serving(c, null)),

        new("Screens/Error", "Error", c => c.Render<ErrorView>(p => p.Add(x => x.OnRestart, () => { })).FindAll("*")),
    ];

    private static IReadOnlyList<IElement> FoodSearch(BunitContext context, string query = "", IReadOnlyList<FoodGroup>? groups = null, Food? selected = null, bool loading = false, bool failed = false, Meal? meal = null, FoodGroupKind[]? expanded = null) =>
        context.Render<FoodSearchView>(p => p
            .Add(x => x.Meal, meal)
            .Add(x => x.OnAdd, (Food _) => { })
            .Add(x => x.OnBack, () => { })
            .Add(x => x.Query, query)
            .Add(x => x.Groups, groups ?? [])
            .Add(x => x.Expanded, (expanded ?? []).ToHashSet())
            .Add(x => x.OnExpand, (FoodGroupKind _) => { })
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
        TimeOnly? suggestedAt = null, bool saving = false, bool saveFailed = false, bool loading = false, bool loadFailed = false, bool editing = false, bool removeFailed = false, bool entryGone = false) =>
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
            .Add(x => x.Editing, editing)
            .Add(x => x.Date, DashboardSamples.Today)
            .Add(x => x.RemoveFailed, removeFailed)
            .Add(x => x.EntryGone, entryGone)
            .Add(x => x.OnSave, () => { })
            .Add(x => x.OnRemove, () => { })
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

    private static IReadOnlyList<IElement> Dashboard(BunitContext context, DaySummary? summary, DateOnly? date = null, bool loading = false, bool loadFailed = false, string? snackbar = null) =>
        Posted(context, snackbar).Render<DashboardView>(p => p
            .Add(x => x.Date, date ?? DashboardSamples.Today)
            .Add(x => x.Today, DashboardSamples.Today)
            .Add(x => x.Summary, summary)
            .Add(x => x.Loading, loading)
            .Add(x => x.LoadFailed, loadFailed)
            .Add(x => x.OnAdd, (MealAddRequest _) => { })
            .Add(x => x.OnEdit, (EntryEditRequest _) => { })
            .Add(x => x.OnRetry, () => { })
            .Add(x => x.OnSettings, () => { })
            .Add(x => x.OnNavigate, (NavDestination _) => { }))
            .FindAll("*");

    /// <summary>Posts the message with Undo to the context's snackbar before the screen renders, as another screen would; returns the context.</summary>
    private static BunitContext Posted(BunitContext context, string? message)
    {
        if (message is not null)
        {
            context.Services.GetRequiredService<SnackbarService>().Show(message, "Undo", () => Task.CompletedTask, Timeout.InfiniteTimeSpan);
        }

        return context;
    }
}
