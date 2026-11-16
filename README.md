<div align="center">

<img src="docs/images/readme/icon.svg" alt="Calorie Tracker app icon" width="112" />

# Calorie Tracker

**A calorie and meal tracker for Android, built to work offline.**

[![Latest release](https://img.shields.io/github/v/release/marty-shields/calorie-tracker-app?label=release)](https://github.com/marty-shields/calorie-tracker-app/releases/latest)
![Android 16](https://img.shields.io/badge/Android-16_(API_36)-34A853?logo=android&logoColor=white)
![.NET 11](https://img.shields.io/badge/.NET-11-512BD4?logo=dotnet&logoColor=white)
![MAUI Blazor Hybrid](https://img.shields.io/badge/MAUI-Blazor_Hybrid-512BD4?logo=blazor&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-on_device-003B57?logo=sqlite&logoColor=white)
[![Stories site](https://img.shields.io/badge/stories-GitHub_Pages-305252?logo=storybook&logoColor=white)](https://marty-shields.github.io/calorie-tracker-app/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[What's here](#whats-here) · [Install](#install) · [Built with](#built-with) · [Getting started](#getting-started) · [License](#license)

</div>

---

## What's here

This is the architecture, design system, tests and CI of the app, with a day diary on top. It starts on the Dashboard, and these screens are the main ones:

- **Dashboard**: the day diary, with a date strip, calories and macros, and four meals to add food to.
- **Foods** and **Scan**: find a food by name or barcode and add it to a meal.
- **Settings**: the theme (System, Light or Dark), the daily calorie target and the app version.

More diary features are still to come. [AGENTS.md](AGENTS.md) lists the rules the code follows, and the [design system](https://marty-shields.github.io/calorie-tracker-app/) shows the tokens, components and screens.

## Install

The app is sideloaded: it isn't on Google Play. Once a release exists:

1. Download `CalorieTracker-X.Y.Z.apk` from the [latest release](https://github.com/marty-shields/calorie-tracker-app/releases/latest).
2. Check it's signed with the release key: `apksigner verify --print-certs CalorieTracker-X.Y.Z.apk` (Android SDK build tools) must show the SHA-256 digest at the top of the release notes.
3. Open it on the phone (allow your file manager to install apps), or run `adb install -r CalorieTracker-X.Y.Z.apk`. A newer version installs over the old one and keeps your data.

Cutting the first release needs the keystore and the `CERT_SHA256` pin: see [Release](docs/release.md).

## Your data

- Data lives in a SQLite database on the phone. Scanning a barcode the app hasn't stored sends that barcode to an external product database (Open Food Facts), and the app itself sends none of your food log or other data anywhere. The camera scanner is Google ML Kit, which may send usage metrics to Google as set by Google's terms.
- Android Auto Backup copies that database, and only that, to your Google account, and a phone-to-phone transfer carries it too. The Dev build is never backed up.
- Logs are in `Android/data/<package id>/files/logs`, readable over USB or `adb pull`. They hold events, ids, counts and non-personal data such as a scanned barcode, never personal information ([`Logging.feature`](tests/CalorieTracker.E2E.Tests/Features/Logging.feature) checks the start-up lines).

## Built with

| Area | Choice |
|---|---|
| App | [.NET MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui/user-interface/controls/blazorwebview) on .NET 11: Razor components in a native Android shell |
| Platform | Android 16 (API 36), sideloaded |
| Data | SQLite via sqlite-net-pcl, on the device |
| Design system | Design tokens, components and screens, documented in a [BlazingStory](https://github.com/jsakamoto/BlazingStory) stories site, [published on GitHub Pages](https://marty-shields.github.io/calorie-tracker-app/) |
| Tests | NUnit unit tests, bUnit component tests, and end-to-end Gherkin features run by Reqnroll and Appium on a phone or emulator |
| CI | GitHub Actions builds and tests every pull request, end-to-end tests on an emulator included; the release workflow runs them again, then signs and publishes the APK; Dependabot keeps packages current |

### Project layout

```
src/
  CalorieTracker.App/          MAUI Blazor Hybrid host (Android): startup, DI, routing
  CalorieTracker.UI/           Razor class library: screens, components, design tokens
  CalorieTracker.UI.Stories/   BlazingStory stories site for the UI
  CalorieTracker.Core/         Data, schema upgrades, food lookup contracts (Foods), services, logging
tests/
  CalorieTracker.Core.Tests/   NUnit tests for Core
  CalorieTracker.UI.Tests/     bUnit tests for components and screens
  CalorieTracker.E2E.Tests/    Gherkin features on a device (Reqnroll + Appium)
```

## Getting started

You need the .NET 11 SDK with the `maui-android` workload. The full setup, including the Android SDK and the phone, is in [Dev setup & build](docs/dev-setup-build.md).

```powershell
# Build (warnings fail the build)
dotnet build CalorieTracker.slnx

# Run the unit and component tests
dotnet test tests/CalorieTracker.Core.Tests
dotnet test tests/CalorieTracker.UI.Tests

# Browse the stories site at http://localhost:5056 (main is at https://marty-shields.github.io/calorie-tracker-app/)
dotnet run --project src/CalorieTracker.UI.Stories

# Run on a connected Android phone
dotnet build src/CalorieTracker.App -t:Run -f net11.0-android
```

The end-to-end tests need a connected device or an emulator: see [E2E tests](docs/e2e-tests.md).

## License

[MIT](LICENSE).
Third-party notices are in [NOTICE](NOTICE).
