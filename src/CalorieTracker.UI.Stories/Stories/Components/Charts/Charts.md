---
$attribute: CustomPage("Components/Charts/Overview")
---

# Charts

## `CalorieRing`
A ring for the day's calories. With a `Target` the centre shows the calories left ("1,441 kcal left"); with none it shows calories eaten ("659 kcal eaten"; negative `Eaten` counts as 0); over the target it shows the amount over, in `--color-error` ("259 kcal over"). The arc is `--color-accent` on a `--color-ring-track` track and fills with `Eaten` / `Target`, full at or over the target. A target of null or 0 counts as none. `--ring-size` across, `--ring-width` thick.
- Text alternative: the ring is `role="img"` with an `aria-label` carrying the numbers, e.g. "1,441 kcal left. Eaten 659 of 2,100 kcal target." or "659 kcal eaten. No target set."
- `data-testid`: `TestId`; the number `{TestId}-value`, the words under it `{TestId}-label`.

## `EnergyShareBar`
A bar with one segment each for protein, carbs and fat, sized by their share of the calories (4 kcal per gram of protein and carbs, 9 per gram of fat), with the grams under it: "Protein 30 g", "Carbs 67 g", "Fat 29 g". The segments use `--color-protein`, `--color-carbs` and `--color-fat`, a `--space-1` gap apart; a macro at zero gets no segment, and the three whole percentages always add up to 100; each label has a dot in its colour. With all three at zero the bar is an empty `--color-border` track.
- Text alternative: the bar is `role="img"` with an `aria-label` carrying the grams and each share, e.g. "Energy share: protein 30 g, 19% of calories; carbs 67 g, 41%; fat 29 g, 40%." The colours are never the only way to tell the segments apart.
- `data-testid`: `TestId`; the labels `{TestId}-protein`, `-carbs`, `-fat`; the segments `{TestId}-protein-segment`, `-carbs-segment`, `-fat-segment`.

Stories: [CalorieRing](./?path=/docs/components-charts-caloriering--docs) · [EnergyShareBar](./?path=/docs/components-charts-energysharebar--docs)
