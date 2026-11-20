---
$attribute: CustomPage(StoryTitles.Prefix + "Foundations/Introduction")
---

# Calorie Tracker design system

Tokens, components, screens and their behaviour for the Calorie Tracker app.

## Where it lives
| Item | Location |
|---|---|
| Tokens | the Shields.UI kit's `tokens.css`, then this app's own `src/CalorieTracker.UI/wwwroot/css/tokens.css` (CSS custom properties) |
| General components (buttons, inputs, list rows, cards, the bottom bar, the phone frame) | the Shields.UI kit; its stories are on the kit's stories site |
| This app's components | `src/CalorieTracker.UI/Components/` (Razor components with scoped `.razor.css`) |
| Screens | `src/CalorieTracker.UI/Screens/` (`*View` components, and `*Page` components that call services) |
| Stories and these pages | `src/CalorieTracker.UI.Stories/Stories/` (a library): `Foundations/`, `Components/<Group>/`, `Screens/`, all under the `Calorie Tracker` section; `src/CalorieTracker.UI.Stories.Host` is the site that shows them |

## Sections
- **Foundations**: this page, [Tokens](./?path=/story/calorie-tracker-foundations-tokens--all-tokens), [Navigation](./?path=/custom/calorie-tracker-foundations-navigation) and the [App Icon](./?path=/custom/calorie-tracker-foundations-app-icon-overview).
- **Components**: this app's own components, one folder per group, with an Overview page and one stories file per component. Each story is one visual state.
- **Screens**: one folder per screen, with an Overview page (parts, behaviour, `data-testid`s) and one story per state, in the phone frame.

## Rules
- Light and dark themes. The app follows the phone's setting (`prefers-color-scheme`) until Settings, Theme sets Light or Dark.
- Components use tokens only; no hard-coded colours or sizes. Story-only chrome (phone frame, theme switch) is exempt.
- Components take their data as parameters. Pages call services.
- Numbers use the phone's numeric keyboard: `inputmode="numeric"` for whole numbers, `inputmode="decimal"` for decimals.
- Content stays inside the Android 16 safe-area insets: `--inset-top` and `--inset-bottom`. The app sets them to the space the status bar, camera cutout and navigation bar cover; elsewhere they are `env(safe-area-inset-*)`, and the phone frame sets 32px.
- Layout suits one-handed use.
- Interactive elements have a `data-testid`.

## Tokens
Defined in the two `tokens.css` files; the [Tokens](./?path=/story/calorie-tracker-foundations-tokens--all-tokens) story shows each one in light and dark.

### Palette
Five colours. Light uses them directly; dark keeps aqua for primary buttons and uses neutral, low-tint colours for everything else so text, accents and surfaces read apart:

| Colour | Light | Dark |
|---|---|---|
| Dark teal `#305252` | Top bar, primary buttons, text on secondary buttons | — |
| Aqua `#b7d5d4` | Secondary buttons, card borders, top-bar subtitle | Primary buttons |
| Teal `#488286` | Focus, stat numbers | — |
| Charcoal `#373e40` | Text | — |
| Grey `#77878b` | Input borders | — |

Derived from the palette:

| Colour | Light | Dark |
|---|---|---|
| Background | `#f4f8f8` | `#141819` |
| Cards | `#ffffff` | `#212829` |
| Card borders | Aqua `#b7d5d4` | `#364243` |
| Text | Charcoal `#373e40` | `#e4ecec` |
| Muted text and placeholders | `#5e6e72` | `#a3b1b3` |
| Input borders | Grey `#77878b` | `#7f9093` |
| Top bar | Dark teal `#305252` | `#1b2526` |
| Top-bar text and subtitle | `#ffffff`, aqua `#b7d5d4` | `#e4ecec`, `#a3b1b3` |
| Text on primary buttons | `#ffffff` | `#173535` |
| Secondary buttons | Aqua `#b7d5d4` | `#2b4444` |
| Text on secondary buttons | Dark teal `#305252` | `#d4e8e7` |
| Focus and stat numbers | Teal `#488286` | `#8cc4c1` |
| Danger buttons | `#a8231c` | `#f2b8b5` |
| Error text and the over-target ring | `#841c16` | `#f4c1be` |
| Accent (spinner, ring arc) | `#29494c` | `#acd5d2` |
| Ring track | `#829493` | `#667476` |

The accent is a darkened teal used for the ring and spinner arcs, and error is darker in light and lighter in dark so the over-target ring keeps 3:1 on the track.

Text and controls meet WCAG AA in both themes.

### Other tokens
- Most colour tokens are `light-dark()` pairs. `data-theme="light"` or `"dark"` forces a theme on an element; component and screen stories have a System / Light / Dark switch.
- Danger buttons are filled red; error text uses a deeper red (lighter in dark) so the over-target ring keeps 3:1 on the track.
- System font; body 16px, number and duration inputs 20px; radius 8 / 12 / 20px, plus round; spacing 4–32px.
- Some components override tokens inside them: the top bar sets text, muted text and focus.

## Checks
Tests in `CalorieTracker.UI.Tests` fail when:
- a component's CSS has a literal colour or size, or uses a token that doesn't exist;
- a token pair is below WCAG AA in light or dark;
- a button or input is smaller than the touch target;
- an interactive element on a screen state has no `data-testid`;
- a component has no stories file or more than one, a `*View` is in no screen's stories, or a stories file shows anything else;
- a screen's stories differ from the screen states listed in the tests;
- a component has no section on its group's Overview, or a `*View` isn't named on a screen's Overview;
- the app icon or splash uses a colour outside the palette and white.
