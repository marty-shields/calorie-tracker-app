---
$attribute: CustomPage("Screens/Scan/Overview")
---

# Scan

Scans a product's barcode and shows what it contains. View: `ScanView`; page: `ScanPage`. Logging the food to a diary is out of scope.

| Part | Content |
|---|---|
| Top bar | Title "Scan barcode". Scan is a top-level screen: no back arrow, and Android back goes to the Dashboard. |
| Content | **Scanning**: the camera with "Point the camera at a barcode". **Looking up**: a spinner. **Found**: name, brand (when known) and energy, protein, carbs and fat per 100 g, or per 100 ml for a liquid. **Not found**: "Unable to find this product". **Camera unavailable**: "Camera unavailable. Check camera access in your phone's settings, then try again." (permission refused, or the scanner could not open). |
| Warning | Under the values when kcal differs from 4 × protein + 4 × carbs + 9 × fat by more than 20% (a placeholder to confirm). |
| Bottom bar | **Scan again** on Found, Not found and Camera unavailable. |
| Navigation bar | `BottomNav` with Scan current. |

The camera stays open until it sees a barcode of a valid length (8, 12 or 13 digits); other codes are ignored. That one barcode is normalised and looked up, and later detections are ignored: the scanner closes on the first accepted code, so no separate in-flight guard is needed. Not found, offline and rate limited all show the one not-found message, with no mention of the source or the reason. Cancelling the camera returns to the Dashboard.

States: [Scanning](./?path=/story/screens-scan--scanning), [Looking up](./?path=/story/screens-scan--looking-up), [Found](./?path=/story/screens-scan--found), [Found liquid](./?path=/story/screens-scan--found-liquid), [Found with warning](./?path=/story/screens-scan--found-with-warning), [Not found](./?path=/story/screens-scan--not-found), [Camera unavailable](./?path=/story/screens-scan--camera-unavailable).

`data-testid`: `scan-camera`, `scan-hint`, `scan-looking-up`, `scan-product`, `scan-name`, `scan-brand`, `scan-per`, `scan-kcal`, `scan-protein`, `scan-carbs`, `scan-fat`, `scan-warning`, `scan-not-found`, `scan-camera-unavailable`, `scan-again`.
