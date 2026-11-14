---
$attribute: CustomPage("Screens/Scan/Overview")
---

# Scan

Scans a product's barcode and shows what it contains. View: `ScanView`. Logging the food to a diary is out of scope.

| Part | Content |
|---|---|
| Top bar | Back arrow and title "Scan barcode". |
| Content | **Scanning**: the camera with "Point the camera at a barcode". **Looking up**: a spinner. **Found**: name, brand (when known) and energy, protein, carbs and fat per 100 g, or per 100 ml for a liquid. **Not found**: "Unable to find this product". |
| Warning | Under the values when kcal differs from 4 × protein + 4 × carbs + 9 × fat by more than 20% (a placeholder to confirm). |
| Bottom bar | **Scan again** on Found and Not found. |

A detected barcode is normalised and looked up once. Detections of the same barcode are ignored while it is in flight. Every status other than Found shows the one message, with no mention of the source or the reason.

States: [Scanning](./?path=/story/screens-scan--scanning), [Looking up](./?path=/story/screens-scan--looking-up), [Found](./?path=/story/screens-scan--found), [Found liquid](./?path=/story/screens-scan--found-liquid), [Found with warning](./?path=/story/screens-scan--found-with-warning), [Not found](./?path=/story/screens-scan--not-found).

`data-testid`: `scan-camera`, `scan-hint`, `scan-looking-up`, `scan-product`, `scan-name`, `scan-brand`, `scan-per`, `scan-kcal`, `scan-protein`, `scan-carbs`, `scan-fat`, `scan-warning`, `scan-not-found`, `scan-again`.
