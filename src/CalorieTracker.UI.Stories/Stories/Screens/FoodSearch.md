---
$attribute: CustomPage("Screens/FoodSearch/Overview")
---

# Food search

Top-level screen, reached from the **Foods** item of the navigation bar (route `/foods`). View: `FoodSearchView`; page: `FoodSearchPage`. Logging a food to a diary is out of scope.

| Part | Content |
|---|---|
| Top bar | Title "Search foods". No back arrow, except while a food's values show. |
| Navigation bar | `BottomNav` with Foods current. |
| Toolbar | Same layout as Exercises in the workout log app: the search box with the search `Icon` and "Search foods", pinned under the top bar with a line under it; the rows scroll beneath. Hidden while a food's values or the load error show. Results update on every keystroke; a newer keystroke cancels the search still running. |
| Loading | On the first launch the food list is still being loaded in the background: the search box shows and a spinner with "Loading foods…" replaces the results. What is typed meanwhile is kept and searched once loading finishes. If loading fails the screen shows "Couldn't search foods" and Try again, which loads again. |
| Nothing typed | "Search for a food" with the hint "Type a name, like oats or cheddar." |
| Rows | One `ListRow` per food, best match first: its name, the brand muted under it when there is one, "N kcal / 100 g" (or "100 ml") muted at the right, and a chevron. Tapping a row shows that food. |
| No results | "No foods match" under the toolbar. |
| Search failed | "Couldn't search foods" and Try again, which repeats the search. |
| Food selected | Tapping a result replaces the list with the `FoodValues` block: the food's name, brand, "Per 100 g" (or "100 ml"), energy, protein, carbs and fat, plus fibre, sugars, saturates and salt when known. **Back to results**, the back arrow and Android back return to the list with the search kept. |

Back: from a food's values, the list; from the list, the Dashboard.

States: [Loading](./?path=/story/screens-foodsearch--loading) · [Nothing typed](./?path=/story/screens-foodsearch--nothing-typed) · [Results](./?path=/story/screens-foodsearch--results) · [No results](./?path=/story/screens-foodsearch--no-results) · [Food selected](./?path=/story/screens-foodsearch--food-selected) · [Search failed](./?path=/story/screens-foodsearch--search-failed)

`data-testid`: `back` (a food's values only), `bottom-nav`, `foods-link`, `food-search-loading`, `food-search-input`, `food-search-idle`, `food-search-none`, `food-search-error`, `food-search-retry`, `food-results`, `food-result-<id>`, `food-detail`, `food-detail-name`, `food-detail-brand`, `food-detail-per`, `food-detail-kcal`, `food-detail-protein`, `food-detail-carbs`, `food-detail-fat`, `food-detail-fibre`, `food-detail-sugars`, `food-detail-saturates`, `food-detail-salt`, `food-detail-close`.
