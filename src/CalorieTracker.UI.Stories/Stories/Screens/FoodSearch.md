---
$attribute: CustomPage("Screens/FoodSearch/Overview")
---

# Food search

Prototype. View: `FoodSearchView`; page: `FoodSearchPage`. Not yet reachable from the Dashboard or a route. Logging a food to a diary is out of scope.

| Part | Content |
|---|---|
| Top bar | Back arrow; title "Search foods". |
| Search box | Fixed under the top bar with a magnifier and the placeholder "Search foods". Results update on every keystroke; a newer keystroke cancels the search still running. |
| Nothing typed | "Search for a food" with the hint "Type a name, like oats or cheddar." |
| Results | One row per food, best match first: name, brand under it when there is one, and "N kcal / 100 g" (or "100 ml") at the right. |
| No results | "No foods found" with the hint "Check the spelling or try fewer words." |
| Search failed | "Couldn't search foods" and Try again, which repeats the search. |
| Food selected | Tapping a result replaces the list with the food's name, brand, "Per 100 g" (or "100 ml"), energy, protein, carbs and fat, plus fibre, sugars, saturates and salt when known. **Back to results** returns to the list with the search kept. |

Back: previous screen.

States: [Nothing typed](./?path=/story/screens-foodsearch--nothing-typed) · [Results](./?path=/story/screens-foodsearch--results) · [No results](./?path=/story/screens-foodsearch--no-results) · [Food selected](./?path=/story/screens-foodsearch--food-selected) · [Search failed](./?path=/story/screens-foodsearch--search-failed)

`data-testid`: `back`, `food-search-input`, `food-search-idle`, `food-search-empty`, `food-search-error`, `food-search-retry`, `food-results`, `food-result-<id>`, `food-detail`, `food-detail-name`, `food-detail-brand`, `food-detail-per`, `food-detail-kcal`, `food-detail-protein`, `food-detail-carbs`, `food-detail-fat`, `food-detail-fibre`, `food-detail-sugars`, `food-detail-saturates`, `food-detail-salt`, `food-detail-close`.
