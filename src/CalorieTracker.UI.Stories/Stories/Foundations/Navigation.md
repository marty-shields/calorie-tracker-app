---
$attribute: CustomPage("Foundations/Navigation")
---

# Navigation

The app opens on [Startup](./?path=/custom/screens-startup-overview), then the Dashboard.

The Dashboard and Foods are the top-level screens: each shows the navigation bar ([`BottomNav`](./?path=/custom/components-layout-overview)) and has no back arrow. Android back from Foods goes to the Dashboard; from the Dashboard it leaves the app. Add a `NavDestination` and its `NavDestinations` entry for each new top-level screen.

| From | Action | To |
|---|---|---|
| Dashboard | Settings gear | Settings |
| Dashboard | Back | Leaves the app |
| Dashboard | Foods item | Foods |
| Foods | Dashboard item, Back | Dashboard |
| Foods (a food open) | Back, Back to results | Foods list |
| Settings | Back | Dashboard |
| Error | Restart | Dashboard |

## Back
- The back arrow and Android back do the same, except with the soft keyboard open: Android back then only hides the keyboard, and the next back acts as below. The back arrow acts at once.
- What back does on each screen is on its page: [Settings](./?path=/custom/screens-settings-overview).
