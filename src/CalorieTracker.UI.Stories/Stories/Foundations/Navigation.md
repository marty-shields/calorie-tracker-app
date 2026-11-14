---
$attribute: CustomPage("Foundations/Navigation")
---

# Navigation

The app opens on [Startup](./?path=/custom/screens-startup-overview), then the Dashboard.

The Dashboard is the only top-level screen for now: it shows the navigation bar ([`BottomNav`](./?path=/custom/components-layout-overview)) and has no back arrow. Android back from the Dashboard leaves the app. Add a `NavDestination` and its `NavDestinations` entry for each new top-level screen.

| From | Action | To |
|---|---|---|
| Dashboard | Settings gear | Settings |
| Dashboard | Back | Leaves the app |
| Settings | Back | Dashboard |
| Error | Restart | Dashboard |

## Back
- The back arrow and Android back do the same, except with the soft keyboard open: Android back then only hides the keyboard, and the next back acts as below. The back arrow acts at once.
- What back does on each screen is on its page: [Settings](./?path=/custom/screens-settings-overview).
