---
$attribute: CustomPage(StoryTitles.Prefix + "Components/Layout/Overview")
---

# Layout

## Screen frame and navigation bar
The frame of every screen (`AppScreen`) and the bottom navigation bar (`BottomNav`, with its raised Add button) come from the shared kit, not from this app: see the kit's own stories for their parts and `data-testid`s. The app adds `NavScreen` (in `Navigation/`), a wrapper that gives the two top-level screens (Today and Trends) the bar from the `NavDestinations` table (label, icon and test id of each `NavDestination`) and a raised **Add** button (icon plus, `aria-haspopup="dialog"`, marked `data-sheet-opener`) that raises `OnAdd`; the screen opens its [`AddSheet`](#addsheet).

- Tapping a tab raises `OnNavigate` with its `NavDestination`; Android back on a top-level screen raises it with `NavDestinations.Start`, or leaves the app from there.
- `data-testid`: `bottom-nav`, `today-link`, `trends-link` (one `{name}-link` per destination), `add-button`.

## `AddSheet`
The modal bottom sheet the bar's Add button opens, on Today and on Trends (both through `AddSheetHost`, which adds the day: the date strip's on Today, today on Trends). Shown while `Open` is set; the screen owns that state.

| Part | Content |
|---|---|
| Frame | The kit's `Sheet` (see its stories): scrim (a tap closes the sheet), a decorative handle, the title "Add food", the enter animation, a maximum width and height, a Tab trap and focus return. |
| Meal chips | Breakfast, Lunch, Dinner, Snacks (`FilterChips`). The meal for the time of day is selected each time the sheet opens: `MealWindows.MealFor`, so breakfast 05:00 to 11:00, lunch 11:00 to 15:00, dinner 17:00 to 21:00, and snacks otherwise (15:00 to 17:00 and 21:00 to 05:00). |
| Tiles | **Search foods** and **Scan barcode**, the kit's `SheetAction` in a two-column grid, each raising its callback (`OnSearch`, `OnScan`) with the selected meal. The screen adds the day on its date strip and opens `/foods?meal=<Meal>&date=<date>` or `/scan?meal=<Meal>&date=<date>`, and closes the sheet. |
| Usual foods | "Usual for <meal>" under the tiles: up to 3 rows (`Usual`), each with the food's title, its last amount and kcal ("150 g · 222 kcal", or "1 serving · ½ pizza (201.8 g) · 521 kcal") and a **+** in a lighter, muted circle (`--color-chip-soft` with a `--color-on-chip-soft` plus, still the 48px target) named "Add <title> to <Meal>". `OnMealChanged` is raised when the sheet opens and when another meal chip is chosen, so the screen sets `Usual` for that meal. Hidden when `Usual` is unset or empty (no history, or the read failed: Search foods and Scan barcode still work). A **+** raises `OnAddUsual` with the food and the meal; the screen adds it with its last amount, servings and serving, closes the sheet and posts "<title> added to <Meal>" with Undo. |
| Add failed | `AddError` shows a line in the sheet saying what failed ("Couldn't add <title> to <Meal>. Try again."); the sheet stays open. |

- The scrim, Android back and Escape raise `OnClose` and navigate nowhere. While it is open, Tab stays inside the sheet, and when it closes focus returns to the Add button (the element marked `data-sheet-opener`).
- The clock is the registered `TimeProvider`; the `Clock` parameter replaces it, which is how each story fixes its time.
- It logs the action (Search, Scan, Add usual or Dismiss) and the meal, never anything else (`UiEvent` 2300 block). The Today and Trends pages log the usual foods read, the add and a failed Undo with food ids, meals, counts and statuses only.
- `data-testid`: `add-sheet`, `add-sheet-scrim`, `add-sheet-handle`, `add-sheet-meals`, `add-sheet-meal-<meal>`, `add-sheet-search`, `add-sheet-scan`, `add-sheet-usual`, `add-sheet-usual-<foodId>`, `add-sheet-usual-<foodId>-add` (also `-title` and `-detail`), `add-sheet-add-error`.

Stories: [AddSheet](./?path=/docs/calorie-tracker-components-layout-addsheet--docs), one for each guessed meal, and with usual foods, without them, with a failed read and with a failed add.
