---
$attribute: CustomPage(StoryTitles.Prefix + "Screens/Settings/Overview")
---

# Settings

Opened from the gear on Today or Trends; Back (the arrow and Android back) returns to the screen that opened it: Trends with the same period and day, or Today on the day it showed (`SettingsRoute`: the `back` query, an in-app path only; any other or missing value goes to Today). View: `SettingsView`; page: `SettingsPage`.

| Part | Content |
|---|---|
| Top bar | Back arrow; title "Settings". |
| Content | Heading "Appearance" and a **Theme** segmented control: System, Light and Dark (System selected by default). |
| Daily target | Heading "Daily target" under Appearance and one field, **Calories per day**, with the unit "kcal". Helper text "Shown on the dashboard. Leave it empty for no target." sits under it. Typing saves at once and emptying the field clears the target (the dashboard then shows calories eaten). A number the target can't be (zero, negative, fractional or above 10,000) or text that isn't a number (such as "abc" or "-") saves nothing, keeps what was typed and shows "Enter a whole number above 0, or clear the field." in the error tone in place of the helper text. If the target can't be read the field shows empty; if it can't be saved the last saved target comes back in the field. |
| Version | "Version 1.0.0": muted, centred and pinned to the bottom of the screen. Nothing shows when there is no version. CI and local E2E builds show "Version 1.0.0-e2e.<run id>" or "1.0.0-e2e.<epoch>". |
| Theme | Tapping a segment applies it at once and saves it on the phone. **System** follows the phone's light or dark setting and is the default. **Light** and **Dark** override it, and are kept across app starts. The saved choice is applied before the first screen shows, so the app never flashes the wrong theme. If the saved choice can't be read, System shows; if the phone fails to apply a tapped choice, the previous one stays selected; if it applies but can't be saved, the new one stays selected until the app closes, then System or the earlier saved choice returns. |

Back: the opener (Trends on the same period and day, or Today on its day), else Today.

States: [Default](./?path=/story/calorie-tracker-screens-settings--default) · [Dark chosen](./?path=/story/calorie-tracker-screens-settings--dark-chosen) · [No version](./?path=/story/calorie-tracker-screens-settings--no-version) · [Target set](./?path=/story/calorie-tracker-screens-settings--target-set) · [Target not set](./?path=/story/calorie-tracker-screens-settings--target-not-set) · [Invalid target](./?path=/story/calorie-tracker-screens-settings--invalid-target)

`data-testid`: `back`, `theme`, `settings-theme-system`, `settings-theme-light`, `settings-theme-dark`, `settings-target` (the input; also `settings-target-unit`, `settings-target-error`), `settings-target-hint`, `app-version`.
