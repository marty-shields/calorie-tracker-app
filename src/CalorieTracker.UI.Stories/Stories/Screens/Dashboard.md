---
$attribute: CustomPage("Screens/Dashboard/Overview")
---

# Dashboard

Home screen: the day diary. A date strip, the day's calories and macros, then the four meals with their entries and a + each. View: `DashboardView`; page: `DashboardPage`.

| Part | Content |
|---|---|
| Top bar | Title "Calorie Tracker"; Settings gear (`IconButton`) on the right, opens Settings. No back arrow; back leaves the app. |
| Date strip | `DateStrip` under the bar: "Today, 19 Jul", the arrows move a day either way with no limit (future days too), and tapping the label returns to today. A future day looks like any other. The page opens on today, or on the day in the route's `date` (`/?date=yyyy-MM-dd`, which the Foods snackbar's **View day** uses). |
| Calories | A `SummaryCard` with the `CalorieRing` and, beside it, "Eaten" and "Target". Calories left; over the target the ring shows how many over, in the error tone. With no target set the ring shows calories eaten, the card says "No target set" and **Set a target** opens Settings. Whole kcal as the day summary rounds them. |
| Macros | The `EnergyShareBar` with protein, carbs and fat in grams. Hidden while nothing is logged. |
| Empty day | `EmptyState` "Nothing logged today" ("Nothing logged this day" on any other day) with "Tap + on a meal to add your first food." shows in place of the macros when no meal has an entry. |
| Meals | Breakfast, Lunch, Dinner and Snacks, always in that order. Each is a `MealHeader` (name, total kcal once it has entries, filled + `IconButton`) and its entries in the order added. A meal with no entries on a day that has some says "Nothing logged". The + opens food search to add to that meal and the day on the strip (`/foods?meal=<Meal>&date=yyyy-MM-dd`). |
| Entries | `ListRow` with the food's title (brand and name, see Food search: "Weetabix Original"), the brand line (the first brand the title lacks) before the amount ("Ferrero · 15 g"), the amount ("40 g", "200 ml") or for an entry added by servings "1 serving · " and the label of the serving it was added by ("1 serving · ½ pizza (201.8 g)"; an entry saved before servings had their own table, or whose serving was deleted, shows "2 servings · 400 ml", the amount, instead), and the calories at the right. Tapping a row (`OnEdit`) opens Serving in edit mode for that entry, to change its amount or meal or remove it (see Serving). |
| Loading | The date strip stays, with a spinner "Loading…" where the day goes. |
| Load failed | The date strip stays, with "Couldn't load this day" and **Try again**. Also shown when an entry's food is missing. |
| Navigation bar | `BottomNav` with **Today** current and the raised **Add** button in the middle; Foods and Scan are not in the bar. Add opens the `AddSheet` (title "Add food", meal chips with the meal for the time of day selected, **Search foods** and **Scan barcode** and up to 3 usual foods of the chosen meal, each added in one tap at its last amount, with the snackbar "<title> added to <Meal>" and Undo; a failed add keeps the sheet open with a message and a failed read hides the section), which sends the chosen meal and the day on the date strip on: `/foods?meal=<Meal>&date=yyyy-MM-dd` or `/scan?meal=<Meal>&date=yyyy-MM-dd`. The scrim, Android back and Escape close it without navigating, and focus returns to Add. See [`AddSheet`](./?path=/docs/components-layout-addsheet--docs). |
| Snackbar | A message posted by another screen, such as "Oat flakes added to Lunch" or, after Remove in Serving's edit mode, "Cheddar cheese removed", with **Undo** (which restores the entry), shows above the navigation bar (`<Snackbar />` in the `Status` slot). Nothing shows when none is posted. |

Dark theme: the same layout; switch the theme in the toolbar to see it.

States: [First run](./?path=/story/screens-dashboard--first-run), [No target](./?path=/story/screens-dashboard--no-target), [Loaded](./?path=/story/screens-dashboard--loaded), [Over target](./?path=/story/screens-dashboard--over-target), [Loading](./?path=/story/screens-dashboard--loading), [Load failed](./?path=/story/screens-dashboard--load-failed), [Future day](./?path=/story/screens-dashboard--future-day), [Branded foods](./?path=/story/screens-dashboard--branded-foods), [Entry removed](./?path=/story/screens-dashboard--entry-removed)

`data-testid`: `date-strip` (`-previous`, `-label`, `-next`), `dashboard-card`, `dashboard-ring` (`-value`, `-label`), `dashboard-eaten`, `dashboard-target`, `dashboard-no-target`, `dashboard-set-target`, `dashboard-share`, `dashboard-empty`, `meal-<meal>` (`-name`, `-calories`, `-add`, `-none`), `entry-<id>`, `dashboard-loading`, `dashboard-load-failed` (`-error`, `-retry`), `bottom-nav`, `today-link`, `add-button`, `add-sheet` (`-scrim`, `-meals`, `-meal-<meal>`, `-search`, `-scan`), `settings`.
