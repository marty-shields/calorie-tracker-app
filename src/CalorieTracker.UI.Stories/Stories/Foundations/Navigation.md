---
$attribute: CustomPage("Foundations/Navigation")
---

# Navigation

The app opens on [Startup](./?path=/custom/screens-startup-overview), then the Dashboard.

The Dashboard (the **Today** item) and [Trends](./?path=/custom/screens-trends-overview) (the **Trends** item, right of Add) are the top-level screens: each shows the navigation bar ([`BottomNav`](./?path=/custom/components-layout-overview)) and has no back arrow. Android back leaves the app from the Dashboard, the start destination, and goes to the Dashboard from Trends. The bar's raised **Add** button is an action, not a destination: it opens the [`AddSheet`](./?path=/custom/components-layout-overview), whose Search foods and Scan barcode open [food search](./?path=/custom/screens-foodsearch-overview) and [Scan](./?path=/custom/screens-scan-overview) for a meal and a day, each with a back arrow. Add a `NavDestination` and its `NavDestinations` entry for each new top-level screen.

| From | Action | To |
|---|---|---|
| Dashboard | Settings gear | Settings |
| Dashboard | Back | Leaves the app |
| Dashboard | Add | Add sheet |
| Dashboard | Trends item | Trends (the current week) |
| Trends | Today item, Back | Dashboard |
| Trends | Settings gear | Settings |
| Trends | A day's bar or calendar cell | Dashboard on that day |
| Trends | Add | Add sheet (adds to today) |
| Add sheet | Scrim, Back, Escape | Dashboard (the sheet closes, focus returns to Add) |
| Add sheet | Search foods | Food search for the chosen meal and day |
| Add sheet | Scan barcode | Scan for the chosen meal and day |
| Food search | Back | Dashboard |
| Food search (a food open, no meal) | Back, Back to results | Food search list |
| Scan | Back, or the camera cancelled | Dashboard |
| Settings | Back | Dashboard |
| Error | Restart | Dashboard |

## Back
- The back arrow and Android back do the same, except with the soft keyboard open: Android back then only hides the keyboard, and the next back acts as below. The back arrow acts at once.
- What back does on each screen is on its page: [Settings](./?path=/custom/screens-settings-overview).
