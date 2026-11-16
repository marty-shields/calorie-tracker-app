using OpenQA.Selenium;

namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>Food search: the search box, the results, and one food's values.</summary>
public sealed class FoodSearchScreen(WebView web)
{
    private string? lastSearch;

    public const string Title = "Search foods";

    /// <summary>Whether a screen title is food search in add mode ("Add to Lunch").</summary>
    public static bool IsAddTitle(string title) => title.StartsWith("Add to ", StringComparison.Ordinal);

    public string ScreenTitle() => web.Text("screen-title");

    public bool HasNavigationBar() => web.Exists("bottom-nav");

    /// <summary>The first launch loads the food list in the background; give it longer than a screen change.</summary>
    private static readonly TimeSpan LoadTime = TimeSpan.FromSeconds(90);

    /// <summary>Waits until the food list has loaded: the search box shows and the spinner is gone.</summary>
    public void WaitUntilLoaded() =>
        web.WaitUntil(() => web.Exists("food-search-input") && !web.Exists("food-search-loading"), "the foods didn't finish loading", LoadTime);

    /// <summary>Types the text into the search box once the foods have loaded.</summary>
    public void Search(string text)
    {
        WaitUntilLoaded();
        lastSearch = text;
        web.Type("food-search-input", text);
    }

    /// <summary>Taps a type chip (ingredients, foods, dishes, drinks or all) and waits until it is chosen.</summary>
    public void ChooseType(string chip)
    {
        var radio = $"food-filter-{chip}";
        web.WaitUntil(() => web.Exists(radio), $"the {chip} chip didn't show");
        web.Find(radio).FindElement(By.XPath("..")).Click();
        web.Eventually(() => web.Find(radio).Selected, Is.True, $"{chip} chip chosen");
    }

    /// <summary>Whether a type chip (ingredients, foods, dishes, drinks or all) is the chosen one.</summary>
    public bool IsTypeChosen(string chip) => web.Find($"food-filter-{chip}").Selected;

    /// <summary>The labels of the type chips, in the order they show.</summary>
    public IReadOnlyList<string> TypeChipLabels()
    {
        web.WaitUntil(() => web.Exists("food-filter"), "the type chips didn't show");
        return web.FindAll("[data-testid=\"food-filter\"] .chip").Select(chip => chip.Text.Trim()).ToList();
    }

    public string PromptText() => web.Text("food-search-idle-message");

    public string NoneText() => web.Exists("food-search-none-message") ? web.Text("food-search-none-message") : web.Text("food-search-none");

    /// <summary>The text of each result row, in order, once at least one shows.</summary>
    public IReadOnlyList<string> ResultRows()
    {
        web.WaitUntil(() => web.Exists("food-results"), "no results showed");
        return web.FindAll("[data-testid^=\"food-result-\"]").Select(row => row.Text.Trim()).ToList();
    }

    /// <summary>The type at the start of each result row's detail line, in order, once at least one shows.</summary>
    public IReadOnlyList<string> ResultTypes()
    {
        web.WaitUntil(() => web.Exists("food-results"), "no results showed");
        return web.FindAll("[data-testid^=\"food-result-\"]").Select(row => row.FindElement(By.CssSelector(".detail")).Text.Split('·')[0].Trim()).ToList();
    }

    /// <summary>Clicks the first row once the box holds what was typed; a row replaced by a newer search is found and clicked again.</summary>
    public void OpenFirstResult() =>
        web.WaitUntil(() =>
        {
            if (lastSearch is not null && Typed() != lastSearch)
            {
                return false;
            }

            var first = web.FindAll("[data-testid^=\"food-result-\"]").FirstOrDefault();
            first?.Click();
            return first is not null;
        }, "no result to open");

    public bool ShowsDetail() => web.Exists("food-detail");

    public string DetailName() => web.Text("food-detail-name");

    public string DetailEnergy() => web.Text("food-detail-kcal");

    public void AddToDiary() => web.Tap("food-detail-add");

    public void BackToResults() => web.Tap("food-detail-close");

    /// <summary>What is in the search box.</summary>
    public string Typed() => web.Value("food-search-input");
}
