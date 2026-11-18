---
$attribute: CustomPage("Components/Layout/Overview")
---

# Layout

## `AppScreen`
The frame of every screen:

| Part | Content |
|---|---|
| Top bar | Back arrow `IconButton` (when `OnBack` is set; Android back raises `OnBack` too), title, subtitle, `Actions` on the right. Dark teal bar; sets text, muted text and focus to the on-bar colours. |
| Content | Scrolls. 16px padding. |
| Bottom bar | `BottomBar` actions, in thumb reach. Optional. |
| Navigation bar | `BottomNav` under the bottom bar, when `Destination` is set; its Add button raises `OnAdd`. |
| Status | `Status`, a message (such as a snackbar) just above the bottom bar (or the navigation bar), over the content. Its slot is a polite live region (`role="status"`), there whenever `Status` is set, so a message shown later is read out. While a status message shows, the content gets room at its foot (`2 × --touch-target` more) so its last controls scroll clear of it. Needs a bottom bar or a navigation bar. Optional. |

- `Destination` makes the screen top-level: it shows the `BottomNav` with that item current, raises `OnNavigate` with the item tapped and `OnAdd` with a tap on Add. A top-level screen sets no `OnBack`, so it has no back arrow and Android back leaves the app, unless it layers a view over itself, where `OnBack` is shown and takes Android back.
- Top padding includes the safe-area inset (`--inset-top`). The bottom inset (`--inset-bottom`) goes under the last thing on screen: the navigation bar, else the bottom bar, else the content.
- `OnRefresh` makes the content pull-to-refresh. Pulled down from the top, a ring like the `Spinner`'s follows the finger at half its travel, coming out from under the top bar. Let go, it goes back up over `--pull-refresh-return-duration`, and if it was past `--pull-refresh-threshold`, `OnRefresh` runs. The ring shows only while pulling, so the screen shows its own loading state, as on first open: a screen shows its `Spinner` "Loading…" in place of the content. A pull during a refresh does nothing. The [Pulling](./?path=/story/components-layout-appscreen--pulling) story shows the ring mid-pull. With reduced motion the ring doesn't slide back. The content doesn't overscroll, so Android's stretch doesn't fight the pull. No gesture without `OnRefresh`.
- `data-testid`: `back` (or `BackTestId`, for a screen layered over another), `screen-title`, `screen-subtitle`, `bottom-bar`, `pull-refresh` (with `OnRefresh`), plus `BottomNav`'s.

Stories: [AppScreen](./?path=/docs/components-layout-appscreen--docs)

## `BottomNav`
The Material navigation bar for the top-level screens, with a raised **Add** button in the middle. The items come from the `NavDestinations` table (label, icon, test id and route of each `NavDestination`): today only Today; Trends is added with its screen. Foods and Scan are not in the bar: they open from the Add sheet.

| Part | Content |
|---|---|
| Item | Icon over its label, sharing the width of its side, at least 48px tall. Today: the `today` calendar. Add an item per top-level screen. |
| Current item | A `--color-selected` pill behind the icon, label and icon in the text colour and bold; the others are muted. `aria-current="page"`. |
| Add | A 56px (`--add-button-size`) `--color-primary` circle with a plus, lifted `--add-button-lift` above the bar, labelled "Add" under it, centred between the two sides. An action, not a destination: it never shows as current, and it has `aria-haspopup="dialog"`. |

- Tapping an item raises `OnNavigate` with its destination. Tapping the current item does nothing. Tapping Add raises `OnAdd`; the screen opens its [`AddSheet`](#addsheet).
- A `<nav>` labelled "Main"; each item is a button. A pressed item shows the shared state layer on its indicator pill, not across the whole item and the inset; Add shows it on its circle.
- Its bottom padding is the safe-area inset (`--inset-bottom`).
- `data-testid`: `bottom-nav`, `today-link` (one `{name}-link` per destination), `add-button`.

Stories: [BottomNav](./?path=/docs/components-layout-bottomnav--docs)

## `AddSheet`
The modal bottom sheet the bar's Add button opens, on Today (and on Trends when it has one). Shown while `Open` is set; the screen owns that state.

| Part | Content |
|---|---|
| Scrim | `--color-scrim` over the screen. A tap closes the sheet. |
| Title | "Add food". |
| Meal chips | Breakfast, Lunch, Dinner, Snacks (`FilterChips`). The meal for the time of day is selected each time the sheet opens: `MealWindows.MealFor`, so breakfast 05:00 to 11:00, lunch 11:00 to 15:00, dinner 17:00 to 21:00, and snacks otherwise (15:00 to 17:00 and 21:00 to 05:00). |
| Tiles | **Search foods** and **Scan barcode**, each raising its callback (`OnSearch`, `OnScan`) with the selected meal. The screen adds the day on its date strip and opens `/foods?meal=<Meal>&date=<date>` or `/scan?meal=<Meal>&date=<date>`, and closes the sheet. |

- The scrim, Android back and Escape raise `OnClose` and navigate nowhere. While it is open, Tab stays inside the sheet, and when it closes focus returns to the Add button (the element marked `data-sheet-opener`).
- The clock is the registered `TimeProvider`; the `Clock` parameter replaces it, which is how each story fixes its time.
- It logs the action (Search, Scan or Dismiss) and the meal, never anything else (`UiEvent` 2300 block).
- `data-testid`: `add-sheet`, `add-sheet-scrim`, `add-sheet-meals`, `add-sheet-meal-<meal>`, `add-sheet-search`, `add-sheet-scan`.

Stories: [AddSheet](./?path=/docs/components-layout-addsheet--docs), one for each guessed meal.

## `SummaryCard`
A card for a summary at the top of a screen: its content on `--color-surface`, with a `--color-border` line and `--radius-lg` corners, padded `--space-4`.
- `data-testid`: `TestId`.

Stories: [SummaryCard](./?path=/docs/components-layout-summarycard--docs)

## `StartupGate`
Runs `Initialize` once at startup. Shows the [Startup](./?path=/custom/screens-startup-overview) screen while it runs or if it throws, then `ChildContent`.

Stories: [StartupGate](./?path=/docs/components-layout-startupgate--docs)

## `AppErrorBoundary`
Shows `ChildContent` until it throws an unhandled error, then logs the error and shows the [Error](./?path=/custom/screens-error-overview) screen. **Restart** raises `OnRestart`, then shows `ChildContent` again.

Stories: [AppErrorBoundary](./?path=/docs/components-layout-apperrorboundary--docs)
