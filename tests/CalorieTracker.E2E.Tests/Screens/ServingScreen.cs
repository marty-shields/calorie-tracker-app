namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The Serving screen: the amount, the meal and Add.</summary>
public sealed class ServingScreen(WebView web)
{
    /// <summary>Waits for the screen to show with its food loaded, after a tap on Add to diary.</summary>
    public void WaitUntilOpen() => web.WaitUntil(() => web.Exists("serving-add"), "the Serving screen didn't show");

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

    /// <summary>Enters a weight, switching to Grams first when the food has a serving, so the amount does not depend on the food.</summary>
    public void EnterGrams(string text)
    {
        if (HasModeSwitch())
        {
            web.Tap("serving-mode-grams");
        }

        EnterAmount(text);
    }

    /// <summary>The label card of a food with one serving.</summary>
    public string ServingLabel() => web.Text("serving-label");

    /// <summary>The weight line of Servings mode, such as "= 40 g".</summary>
    public string Weight() => web.Text("serving-weight");

    /// <summary>The servings of the list a food with two or more has, in the order shown; empty when there is no list.</summary>
    public IReadOnlyList<string> ServingOptions() =>
        web.FindAll("[data-testid=\"serving-select\"] option").Select(option => option.Text.Trim()).ToList();

    public void ChooseServing(string label) => web.Choose("serving-select", label);

    /// <summary>Opens the own-serving form, fills it and saves, then waits for the form to close.</summary>
    public void AddOwnServing(string label, string amount)
    {
        web.Tap("serving-own-add");
        web.Type("serving-own-label", label);
        web.Type("serving-own-amount", amount);
        web.Tap("serving-own-save");
        web.WaitUntil(() => !web.Exists("serving-own-label"), $"the form for the serving {label} didn't close");
    }

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

    public void Back() => web.Tap("back");

    public string NotFoundText() => web.Text("serving-not-found-message");

    /// <summary>Waits for edit mode to show with its entry loaded, after a tap on an entry.</summary>
    public void WaitUntilEditing() => web.WaitUntil(() => web.Exists("serving-save"), "the Serving screen didn't open in edit mode");

    public void ChooseMeal(string meal)
    {
        web.Tap($"serving-meal-{meal.ToLowerInvariant()}");
        web.Eventually(() => IsMealChosen(meal), Is.True, $"{meal} chosen");
    }

    public string SaveText() => web.Text("serving-save");

    public bool HasAddButton() => web.Exists("serving-add");

    public string RemoveLabel() => web.Attribute("serving-remove", "aria-label");

    /// <summary>The muted meal and day line of edit mode, such as "Lunch · 15 Nov".</summary>
    public string When() => web.Text("serving-when");

    public void Save() => web.Tap("serving-save");

    public void Remove() => web.Tap("serving-remove");
}
