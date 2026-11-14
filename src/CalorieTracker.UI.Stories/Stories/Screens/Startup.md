---
$attribute: CustomPage("Screens/Startup/Overview")
---

# Startup

Shown while the app opens the database, before the Dashboard. View: `StartupView`, shown by `StartupGate`.

| Part | Content |
|---|---|
| Top bar | Title "Calorie Tracker". |
| Content | Centred. `Spinner` "Loading…" while loading; "Couldn't open the database" with a **Try again** button when `Failed`; Try again runs startup again. |

States: [Loading](./?path=/story/screens-startup--loading) · [Load failed](./?path=/story/screens-startup--load-failed)

`data-testid`: `startup-loading`, `startup-error`, `startup-retry`.
