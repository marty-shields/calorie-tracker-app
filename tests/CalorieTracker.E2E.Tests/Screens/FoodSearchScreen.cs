namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>Food search: the search box, the results, and one food's values.</summary>
public sealed class FoodSearchScreen(WebView web)
{
    public const string Title = "Search foods";

    /// <summary>The first launch loads the food list in the background; give it longer than a screen change.</summary>
    private static readonly TimeSpan LoadTime = TimeSpan.FromSeconds(90);

    /// <summary>Waits until the food list has loaded: the search box shows and the spinner is gone.</summary>
    public void WaitUntilLoaded() =>
        web.WaitUntil(() => web.Exists("food-search-input") && !web.Exists("food-search-loading"), "the foods didn't finish loading", LoadTime);

    /// <summary>Types the text into the search box once the foods have loaded.</summary>
    public void Search(string text)
    {
        WaitUntilLoaded();
        web.Type("food-search-input", text);
    }

    public string PromptText() => web.Text("food-search-idle-message");

    public string NoneText() => web.Text("food-search-none");

    /// <summary>The text of each result row, in order, once at least one shows.</summary>
    public IReadOnlyList<string> ResultRows()
    {
        web.WaitUntil(() => web.Exists("food-results"), "no results showed");
        return web.FindAll("[data-testid^=\"food-result-\"]").Select(row => row.Text.Trim()).ToList();
    }

    public void OpenFirstResult()
    {
        web.WaitUntil(() => web.Exists("food-results"), "no results showed");
        web.FindAll("[data-testid^=\"food-result-\"]").First().Click();
    }

    public bool ShowsDetail() => web.Exists("food-detail");

    public string DetailName() => web.Text("food-detail-name");

    public string DetailEnergy() => web.Text("food-detail-kcal");

    public void BackToResults() => web.Tap("food-detail-close");

    /// <summary>What is in the search box.</summary>
    public string Typed() => web.Value("food-search-input");
}
