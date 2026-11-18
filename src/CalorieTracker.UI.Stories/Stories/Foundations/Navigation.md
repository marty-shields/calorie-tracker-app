---
$attribute: CustomPage("Foundations/Navigation")
---

# Navigation

The app opens on [Startup](./?path=/custom/screens-startup-overview), then Today (`DashboardPage`, `/`).

Today (the **Today** item; `DashboardPage` in the code) and [Trends](./?path=/custom/screens-trends-overview) (the **Trends** item, right of Add) are the top-level screens: each shows the navigation bar ([`BottomNav`](./?path=/custom/components-layout-overview)) and has no back arrow. Android back leaves the app from Today, the start destination, and goes to Today from Trends. The bar's raised **Add** button is an action, not a destination: it opens the [`AddSheet`](./?path=/custom/components-layout-overview), whose Search foods and Scan barcode open [food search](./?path=/custom/screens-foodsearch-overview) and [Scan](./?path=/custom/screens-scan-overview) for a meal and a day, each with a back arrow. Add a `NavDestination` and its `NavDestinations` entry for each new top-level screen.

| From | Action | To |
|---|---|---|
| Today | Settings gear | Settings |
| Today | Back | Leaves the app |
| Today | Add | Add sheet |
| Today | Trends item | Trends (the current week) |
| Trends | Today item, Back | Today |
| Trends | Settings gear | Settings |
| Trends | A day's bar or calendar cell | Today on that day |
| Trends | Add | Add sheet (adds to today) |
| Add sheet | Scrim, Back, Escape | The screen it opened on, Today or Trends (the sheet closes, focus returns to Add) |
| Add sheet | Search foods | Food search for the chosen meal and day |
| Add sheet | Scan barcode | Scan for the chosen meal and day |
| Food search | Back | Today |
| Scan | Back, or the camera cancelled | Its `back` route (such as the search that opened it), else Today on that day |
| Settings | Back | Today |
| Error | Restart | Today |

## Back
- The back arrow and Android back do the same, except with the soft keyboard open: Android back then only hides the keyboard, and the next back acts as below. The back arrow acts at once.
- What back does on each screen is on its page: [Settings](./?path=/custom/screens-settings-overview).
