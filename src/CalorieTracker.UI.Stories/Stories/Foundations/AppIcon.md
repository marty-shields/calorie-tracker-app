---
$attribute: CustomPage("Foundations/App Icon/Overview")
---

# App icon and splash

The Ember flame: one flat flame with the inner flame cut out, on the design system's deep teal.

| Item | Rule |
|---|---|
| Style | One flame outline with a half-size inner flame cut out as a hole (a single `evenodd` path), and a soft drop shadow offset 2.2 units down |
| Colours | Only the colours in `AppIcon/palette.json`; a test checks the SVGs and the project's `Color` and `DarkColor` values against it. The flame is a vertical gradient from `#ff4d3a` through `#ff7d33` to `#ffb43a` |
| Canvas | 108-unit adaptive-icon canvas. The flame spans x 32–76 and y 21–85, inside Android's 66-unit safe circle (its tip is 0.06 units past the edge, which no launcher mask shows) |
| Background | Flat `#173535`, the project's `MauiIcon` colour. No glow behind the flame; the cutout shows the clean teal |
| Themed icon | Android 13+ tints the monochrome layer as one shape: the flame in white with the inner flame as a hole, and no shadow |
| Splash | The flame alone, no wordmark. Light theme `#f4f8f8` (the app's light page) with a `#c9d9d6` shadow; dark theme `#173535` with the icon's own shadow |
| Builds | Debug and Release use the same art |
| SVG features | The build rasterises the SVGs, so they use only plain paths, solid fills, `linearGradient`, `fill-opacity` and `clipPath`. No `mask`, `radialGradient` or `filter` |

## Files
In `src/CalorieTracker.App/`; the stories project embeds the SVGs for these stories:

| File | Use |
|---|---|
| `Resources/AppIcon/appicon.svg` | Icon background |
| `Resources/AppIcon/appiconfg.svg` | Icon foreground, and the dark splash (the splash's `DarkFile`) |
| `Resources/AppIcon/appicon_monochrome.svg` | Themed icon layer (the icon's `MonochromeFile`) |
| `Resources/AppIcon/palette.json` | Every colour the art may use |
| `Resources/Splash/splash.svg` | Light splash |

## Changing the art
The SVGs, and the README's `docs/images/readme/icon.svg`, are generated. Edit the flame outline, the cutout or the shadow in `.github/scripts/app-icon.mjs` and colours in `palette.json`, then run `node .github/scripts/app-icon.mjs`. It overwrites every file, and rerunning it changes nothing.

Story: [Icon and splash](./?path=/story/foundations-app-icon--icon-and-splash)
