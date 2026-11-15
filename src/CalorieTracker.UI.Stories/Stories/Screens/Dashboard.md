---
$attribute: CustomPage("Screens/Dashboard/Overview")
---

# Dashboard

Home screen. For now it is an empty shell: a title, the Settings gear and a "Nothing here yet" empty state. View: `DashboardView`; page: `DashboardPage`.

| Part | Content |
|---|---|
| Top bar | Title "Calorie Tracker"; Settings gear (`IconButton`) on the right, opens Settings. No back arrow; back leaves the app. |
| Content | Centred `EmptyState` "Nothing here yet". |
| Navigation bar | `BottomNav` with Dashboard current, beside Foods and Scan. |
| Snackbar | A message posted by another screen, such as "Oat flakes added to Lunch" with **Undo**, shows above the navigation bar (`<Snackbar />` in the `Status` slot). Nothing shows when none is posted. |

States: [Empty](./?path=/story/screens-dashboard--empty)

`data-testid`: `dashboard-empty`, `bottom-nav`, `dashboard-link`, `foods-link`, `settings`.
