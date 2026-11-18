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

    /// <summary>Whether any type chip shows. Search has none; this stays to prove it.</summary>
    public bool HasTypeChips() => web.Exists("food-filter");

    public string PromptText() => web.Text("food-search-idle-message");

    public string NoneText()
    {
        web.WaitUntil(() => web.Exists("food-search-none-message") || web.Exists("food-search-none"), "no 'No foods match' message showed");
        return web.Exists("food-search-none-message") ? web.Text("food-search-none-message") : web.Text("food-search-none");
    }

    /// <summary>The text of each result row, in order, once at least one shows.</summary>
    public IReadOnlyList<string> ResultRows()
    {
        web.WaitUntil(() => web.Exists("food-results"), "no results showed");
        return web.FindAll("[data-testid^=\"food-result-\"]").Select(row => row.Text.Trim()).ToList();
    }

    /// <summary>The title heading the first result row, or empty while there is none.</summary>
    public string FirstResultTitle() =>
        web.FindAll("[data-testid^=\"food-result-\"] .text").FirstOrDefault()?.Text.Trim() ?? "";

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

    /// <summary>The group headings on the screen, in order, as the testid keys (favourites, usual, recent, diary, common, branded). Only headings count: a group's "Show N more" row has a testid of its own.</summary>
    public IReadOnlyList<string> Groups() =>
        web.FindAll("h2[data-testid^=\"food-group-\"]").Select(h => h.GetDomAttribute("data-testid")!["food-group-".Length..]).ToList();

    /// <summary>Waits for the screen to settle on exactly these groups, in order.</summary>
    public void WaitForGroups(params string[] keys) =>
        web.Eventually(Groups, Is.EqualTo(keys), "the groups shown");

    /// <summary>The testids of the food rows of one group, in order.</summary>
    public IReadOnlyList<string> RowIds(string group) =>
        web.FindAll($"[data-testid=\"food-group-{group}\"] ~ ul [data-testid^=\"food-result-\"]").Select(row => row.GetDomAttribute("data-testid")!).ToList();

    public string FirstRowText(string group)
    {
        web.WaitUntil(() => RowIds(group).Count > 0, $"the {group} group has no rows");
        return web.FindAll($"[data-testid=\"food-group-{group}\"] ~ ul [data-testid^=\"food-result-\"]").First().Text.Trim();
    }

    public void OpenFirstIn(string group)
    {
        web.WaitUntil(() => RowIds(group).Count > 0, $"the {group} group has no rows");
        web.FindAll($"[data-testid=\"food-group-{group}\"] ~ ul [data-testid^=\"food-result-\"]").First().Click();
    }

    /// <summary>The testid of every food row on the screen, in order.</summary>
    public IReadOnlyList<string> AllRowIds() =>
        web.FindAll("[data-testid^=\"food-result-\"]").Select(row => row.GetDomAttribute("data-testid")!).ToList();

    /// <summary>The N of the group's "Show N more" row, once it shows.</summary>
    public int MoreCount(string group)
    {
        web.WaitUntil(() => web.Exists($"food-group-{group}-more"), $"the {group} group has no Show more row");
        return int.Parse(System.Text.RegularExpressions.Regex.Match(web.Text($"food-group-{group}-more"), @"Show (\d+) more").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public void ShowMore(string group) => web.Tap($"food-group-{group}-more");

    /// <summary>How the "Search online" button shows: "primary" when no stored food matches, "secondary" under the results, or null when it is not offered.</summary>
    public string? OnlineEntry()
    {
        var entry = web.FindAll("[data-testid=\"food-search-online\"]").FirstOrDefault();
        return entry is null ? null : entry.TagName != "button" ? "not a button" : entry.GetDomAttribute("class")!.Contains("primary", StringComparison.Ordinal) ? "primary" : "secondary";
    }

    /// <summary>The first-use notice, or null when it is not shown.</summary>
    public string? OnlineNotice() => web.Exists("food-search-online-notice") ? web.Text("food-search-online-notice") : null;

    public void SearchOnline() => web.Tap("food-search-online");

    public string OnlineHeading()
    {
        web.WaitUntil(() => web.Exists("food-group-online"), "no online group showed");
        return web.Text("food-group-online");
    }

    /// <summary>The text of each online product row, in order, with its NEW marker last when it has one.</summary>
    public IReadOnlyList<string> OnlineRows() =>
        web.FindAll("[data-testid^=\"food-online-\"]").Select(row => row.Text.Replace("\n", " ").Trim()).ToList();

    /// <summary>Taps the online product with this title and pack size, showing "Show N more" first when it hides the row.</summary>
    public void OpenOnlineProduct(string title, string pack) =>
        web.WaitUntil(() =>
        {
            var row = web.FindAll("[data-testid^=\"food-online-\"]").FirstOrDefault(r => r.FindElement(OpenQA.Selenium.By.CssSelector(".text")).Text.Trim() == title && r.Text.Contains(pack, StringComparison.Ordinal));
            row?.Click();
            return row is not null;
        }, $"no online product '{title}' of '{pack}' to open");

    public string OnlineMessage()
    {
        web.WaitUntil(() => web.Exists("food-search-online-error"), "no online message showed");
        return web.Text("food-search-online-error");
    }

    public bool OnlineHasTryAgain() => web.Exists("food-search-online-retry");

    public string OnlineNothingFound()
    {
        web.WaitUntil(() => web.Exists("food-search-online-none"), "no 'Nothing found' message showed");
        return web.Text("food-search-online-none");
    }

    public bool ShowsDetail() => web.Exists("food-detail");

    public string DetailName() => web.Text("food-detail-name");

    public string DetailEnergy() => web.Text("food-detail-kcal");

    public void AddToDiary() => web.Tap("food-detail-add");

    public void BackToResults() => web.Tap("food-detail-close");

    /// <summary>The star on a food's values: its accessible name.</summary>
    public string DetailStarLabel() => web.Attribute("food-detail-favourite", "aria-label");

    /// <summary>The star on a food's values: <c>aria-pressed</c>, "true" when the food is a favourite.</summary>
    public string DetailStarPressed() => web.Attribute("food-detail-favourite", "aria-pressed");

    public void TapDetailStar() => web.Tap("food-detail-favourite");

    /// <summary>Whether a result row holds a star button; none does.</summary>
    public bool RowsShowAStar() => web.FindAll("[data-testid^=\"food-result-\"] button").Count > 0;

    /// <summary>What is in the search box.</summary>
    public string Typed() => web.Value("food-search-input");
}
