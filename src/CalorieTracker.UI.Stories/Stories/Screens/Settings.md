---
$attribute: CustomPage("Screens/Settings/Overview")
---

# Settings

Opened from the Dashboard gear. View: `SettingsView`; page: `SettingsPage`.

| Part | Content |
|---|---|
| Top bar | Back arrow; title "Settings". |
| Content | Heading "Appearance" and a **Theme** segmented control: System, Light and Dark (System selected by default). |
| Data sources | Heading "Data sources" and two muted credit lines: CoFID (Open Government Licence v3.0) and Open Food Facts (Open Database Licence). Plain text, the same in every state. |
| Version | "Version 1.0.0": muted, centred and pinned to the bottom of the screen. Nothing shows when there is no version. CI and local E2E builds show "Version 1.0.0-e2e.<run id>" or "1.0.0-e2e.<epoch>". |
| Theme | Tapping a segment applies it at once and saves it on the phone. **System** follows the phone's light or dark setting and is the default. **Light** and **Dark** override it, and are kept across app starts. The saved choice is applied before the first screen shows, so the app never flashes the wrong theme. If the saved choice can't be read, System shows; if the phone fails to apply a tapped choice, the previous one stays selected; if it applies but can't be saved, the new one stays selected until the app closes, then System or the earlier saved choice returns. |

Back: Dashboard.

States: [Default](./?path=/story/screens-settings--default) · [Dark chosen](./?path=/story/screens-settings--dark-chosen) · [No version](./?path=/story/screens-settings--no-version)

`data-testid`: `back`, `theme`, `settings-theme-system`, `settings-theme-light`, `settings-theme-dark`, `credit-cofid`, `credit-openfoodfacts`, `app-version`.
