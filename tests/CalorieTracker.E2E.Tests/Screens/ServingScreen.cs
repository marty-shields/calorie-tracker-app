namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The Serving screen: the amount, the meal and Add.</summary>
public sealed class ServingScreen(WebView web)
{
    /// <summary>Opens the screen for a food through the router, as Foods and Scan will, and waits for the food to load. Without <paramref name="meal"/> the screen suggests one.</summary>
    public void Open(int foodId, string? meal)
    {
        web.NavigateTo($"/serving/{foodId}" + (meal is null ? "" : $"?meal={meal}"));
        web.WaitUntil(() => web.Exists("serving-add"), "the Serving screen didn't show");
    }

    /// <summary>Opens the route of a food that does not exist and waits for the message.</summary>
    public void OpenMissing(int foodId)
    {
        web.NavigateTo($"/serving/{foodId}");
        web.WaitUntil(() => web.Exists("serving-not-found"), "the Serving screen didn't show");
    }

    public string Title() => web.Text("screen-title");

    public bool HasModeSwitch() => web.Exists("serving-mode");

    public string Amount() => web.Value("serving-amount");

    public string Unit() => web.Text("serving-amount-unit");

    public void EnterAmount(string text) => web.Type("serving-amount", text);

    public string? AmountError() => web.Exists("serving-amount-error") ? web.Text("serving-amount-error") : null;

    /// <summary>The labels of the meal chips, in the order they show.</summary>
    public IReadOnlyList<string> MealLabels() =>
        web.FindAll("[data-testid=\"serving-meal\"] .chip").Select(chip => chip.Text.Trim()).ToList();

    public bool IsMealChosen(string meal) => web.Find($"serving-meal-{meal.ToLowerInvariant()}").Selected;

    public int ChosenMeals() => web.FindAll("[data-testid=\"serving-meal\"] input").Count(radio => radio.Selected);

    public string? Suggestion() => web.Exists("serving-suggestion") ? web.Text("serving-suggestion") : null;

    public string AddText() => web.Text("serving-add");

    public bool CanAdd() => web.IsEnabled("serving-add");

    public string AddsKcal() => web.Text("serving-adds-kcal");

    public bool ShowsAdds() => web.Exists("serving-adds");

    public void Add() => web.Tap("serving-add");

    public string NotFoundText() => web.Text("serving-not-found-message");
}
