---
$attribute: CustomPage("Screens/Scan/Overview")
---

# Scan

Scans a product's barcode and shows what it contains. View: `ScanView`; page: `ScanPage`. A found product goes to the Serving screen with **Add to diary**.

| Part | Content |
|---|---|
| Top bar | Title "Scan barcode". Scan is a top-level screen: no back arrow, and Android back goes to the Dashboard. |
| Content | **Opening camera**: a spinner labelled "Opening camera…" until the native camera covers the screen. **Closing camera**: a spinner labelled "Closing camera…" while the camera slides away after Cancel or Android back, so the screen never says "Opening camera…" on the way out. **Looking up**: a spinner. **Found**: the `FoodValues` block: name, brand (when known) and energy, protein, carbs and fat per 100 g, or per 100 ml for a liquid, plus fibre, sugars, saturates and salt when Open Food Facts has them, and a "Serving: 1 biscuit (15 g)" line when the product has a serving. **Not found**: an `EmptyState` with "Unable to find this product" and the hint "Try another barcode, or search by name.", with no reason. **Camera unavailable**: `LoadFailed` "Couldn't open the camera" with **Try again**, which restarts the scan (permission refused, or the scanner could not open). |
| Warning | Under the values when kcal differs from 4 × protein + 4 × carbs + 9 × fat by more than 20% (a placeholder to confirm). |
| Bottom bar | Found: a primary **Add to diary** above a secondary **Scan again**. Add to diary opens the Serving screen for the scanned food (its id; Serving reads the food and bounds its serving and nutrients) and suggests a meal from the time of day; Back from Serving goes to the home screen (not back into the camera, which would lose the product), and so does a save. Add to diary is left out when the food could not be saved (no id); Search by name takes its place, as for not found. Not found: a primary **Search by name**, which opens the Foods tab, above **Scan again**. Camera unavailable has Try again in place of it. |
| Navigation bar | `BottomNav` with Scan current. |

The camera stays open until it sees a barcode of a valid length (8, 12 or 13 digits); other codes are ignored. That one barcode is normalised and looked up, and later detections are ignored: the scanner closes on the first accepted code, so no separate in-flight guard is needed. Not found, offline and rate limited all show the one not-found message, with no mention of the source or the reason. Cancelling the camera returns to the Dashboard. The native camera screen follows the app's chosen theme (System follows the phone): the page passes the saved choice to the scanner, which sets it as the app theme before opening the camera.

States: [Opening camera](./?path=/story/screens-scan--opening-camera), [Closing camera](./?path=/story/screens-scan--closing-camera), [Looking up](./?path=/story/screens-scan--looking-up), [Found](./?path=/story/screens-scan--found), [Found liquid](./?path=/story/screens-scan--found-liquid), [Found with extra nutrients](./?path=/story/screens-scan--found-with-extra-nutrients), [Found with a serving](./?path=/story/screens-scan--found-with-a-serving), [Found with warning](./?path=/story/screens-scan--found-with-warning), [Not found](./?path=/story/screens-scan--not-found), [Camera unavailable](./?path=/story/screens-scan--camera-unavailable).

`data-testid`: `scan-opening-camera`, `scan-closing-camera`, `scan-looking-up`, `scan-product`, `scan-name`, `scan-brand`, `scan-per`, `scan-kcal`, `scan-protein`, `scan-carbs`, `scan-fat`, `scan-fibre`, `scan-sugars`, `scan-saturates`, `scan-salt`, `scan-serving`, `scan-warning`, `scan-not-found` (the not-found box, with `scan-not-found-message` on its text), `scan-camera-unavailable-error`, `scan-camera-unavailable-retry`, `scan-add`, `scan-search`, `scan-again`.
