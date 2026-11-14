---
$attribute: CustomPage("Screens/Dashboard/Overview")
---

# Dashboard

Home screen. For now it is an empty shell: a title, the Settings gear and a "Nothing here yet" empty state. View: `DashboardView`; page: `DashboardPage`.

| Part | Content |
|---|---|
| Top bar | Title "Calorie Tracker"; Settings gear (`IconButton`) on the right, opens Settings. No back arrow; back leaves the app. |
| Content | Centred `EmptyState` "Nothing here yet". |
| Navigation bar | `BottomNav` with Dashboard current, and Scan. |

States: [Empty](./?path=/story/screens-dashboard--empty)

`data-testid`: `dashboard-empty`, `bottom-nav`, `dashboard-link`, `settings`.
