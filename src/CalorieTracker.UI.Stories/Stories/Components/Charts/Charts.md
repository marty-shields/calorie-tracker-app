---
$attribute: CustomPage("Components/Charts/Overview")
---

# Charts

## `CalorieRing`
A ring for the day's calories. With a `Target` the centre shows the calories left ("1,441 kcal left"); with none it shows calories eaten ("659 kcal eaten"; negative `Eaten` counts as 0); over the target it shows the amount over, in `--color-error` ("259 kcal over"). The arc is `--color-accent` on a `--color-ring-track` track and fills with `Eaten` / `Target`, full at or over the target. A target of null or 0 counts as none. `--ring-size` across, `--ring-width` thick; `Compact` makes it `--ring-size-compact` across and `--ring-width-compact` thick (the Today card's ring).
- Text fit: the number and the words under it stay inside the ring's inner circle (radius = half the size less the band), with at least `--space-1` between the text block's corners and the band, at 100%, 115% and 130% text size. The full ring's number is `--font-size-xl`, the compact ring's `--font-size-md` (`lg` leaves too little gap on "762 kcal eaten" and four-digit values). A wider number steps down a size, counting its commas: 6 characters ("12,345") steps down one size (full `lg`, compact `sm`) and 7 or more ("123,456") a second (full `md`, compact `xs`); shorter numbers keep the base size. The compact ring's words are `--font-size-xs` and its text block sits a little high so the widest line, "kcal eaten", clears the band. `check-story-layout.mjs` measures every ring in every story; the edge-case stories show both sizes side by side: single-digit (7), short and four-digit values, 12,345 and 123,456 as left, over and eaten.
- Text alternative: the ring is `role="img"` with an `aria-label` carrying the numbers, e.g. "1,441 kcal left. Eaten 659 of 2,100 kcal target." or "659 kcal eaten. No target set."
- `data-testid`: `TestId`; the number `{TestId}-value`, the words under it `{TestId}-label`.

## `EnergyShareBar`
A bar with one segment each for protein, carbs and fat, sized by their share of the calories (4 kcal per gram of protein and carbs, 9 per gram of fat), with the grams under it: "Protein 30 g", "Carbs 67 g", "Fat 29 g". The segments use `--color-protein`, `--color-carbs` and `--color-fat`, a `--space-1` gap apart; a macro at zero gets no segment, and the three whole percentages always add up to 100; each label has a dot in its colour. With all three at zero the bar is an empty `--color-border` track.
- Text alternative: the bar is `role="img"` with an `aria-label` carrying the grams and each share, e.g. "Energy share: protein 30 g, 19% of calories; carbs 67 g, 41%; fat 29 g, 40%." The colours are never the only way to tell the segments apart.
- `data-testid`: `TestId`; the labels `{TestId}-protein`, `-carbs`, `-fat`; the segments `{TestId}-protein-segment`, `-carbs-segment`, `-fat-segment`.

## `TrendBarChart`
Inline SVG bars by label (a day, or a month), on one y scale from 0 to the first tick at or above the tallest bar and the band, with ticks at 0, 1k and 2k, and 3k (4k) when a bar or the band needs it. The step grows (2k, 5k, 10k, …) so the scale never has more than four intervals, and there is room above the scale for the tooltip, the value and the over marker. With a `Target` it draws a dashed target line and a `--color-target-band` band from 90% to 110% of the target, and gives each bar a status: under (`--color-chip`, below 90%), on target (`--color-accent`, 90% to 110%) or over (`--color-error`, above 110%, with a triangle marker above the bar so colour is not the only sign). A bar may carry its own `Target` when that day had a different one, and a `Note` ("based on 2 days") that marks it as resting on little data: it is drawn faded (`--opacity-thin`) and the note ends its text alternative. Bars have rounded tops on a zero baseline; only the highest bar shows its value. A bar with no value (null or 0) leaves an empty slot, and a chart with none says "No days logged yet". Long labels are cut with an ellipsis to fit their slot.
- With an `OnSelect` handler each bar is a `role="button"` group with `tabindex="0"`; without one it is a `role="img"` group with no `tabindex` that cannot be focused. Its text alternative holds the date, the value and the status ("Mon 9 Nov, 2,500 kcal, over target"); the visible tooltip on hover and focus shows the date and kcal only (no status), kept inside the chart, while the accessible name keeps the status. A click, or Enter on a focused bar, raises `OnSelect` with the bar's date (Space is not handled: Blazor cannot stop the page scrolling for Space alone without also blocking Tab).
- `data-testid`: `TestId`; parts `{TestId}-band`, `-target`, `-baseline`, `-grid-{kcal}`, `-tick-{kcal}`, `-bar-{date}`, `-column-{date}`, `-marker-{date}`, `-gap-{date}`, `-value-label`, `-empty`.

## `MonthCalendar`
A Monday-first grid of one month's days. Each day shows its status: under (`--color-chip`), on target (`--color-accent`), over (`--color-error`, with a triangle marker), not logged (a dashed `--color-border-strong` outline), or future (dimmed and disabled). Today is outlined in `--color-text`, whatever its status. `Today` defaults to the device's date. A day absent from `Days` counts as not logged, and data on a future day is ignored.
- Each day's accessible name holds the date, the value and the status ("Tue 3 Nov, 2,500 kcal, over target"; "Wed 18 Nov, today, not logged"; "Thu 19 Nov, future"). With an `OnSelect` handler each enabled day is a button in the tab order and a click or Enter raises it with the date; without one every day is plain text.
- `data-testid`: `TestId`; the days `{TestId}-day-{date}`, the over markers `{TestId}-marker-{date}`.

## `StatTile`
A card with a large figure ("1,980") and a caption under it ("avg kcal a day"). With `NotEnoughDays` the figure becomes a dash and "Not enough days yet" appears under the caption, so the tile keeps its place in the layout.
- `data-testid`: `TestId`; `{TestId}-figure`, `-caption`, `-note`.

## `BarList`
Rows of a label, a value and a horizontal `--color-accent` bar on a `--color-border` track. Bar lengths are each value over the largest in the list, or over `Max` (30 for days logged in a month). A long label is cut with an ellipsis; the value text is `ValueText` or the number. With no rows it shows `EmptyText`.
- Text alternative: a list named by `Label`; the bars are decoration, the values are text.
- `data-testid`: `TestId`; `{TestId}-row-{n}`, `-label-{n}`, `-value-{n}`, `-bar-{n}`, `-empty`.

## `ChartLegend`
Swatches with labels, in this order: under, on target, over (with its triangle), not logged (dashed outline), the target line and the target band. The caller lists the entries it needs, in order; they wrap onto more lines when they don't fit.
- `data-testid`: `TestId`; `{TestId}-under`, `-on`, `-over`, `-not-logged`, `-line`, `-band`.

Stories: [CalorieRing](./?path=/docs/components-charts-caloriering--docs) · [EnergyShareBar](./?path=/docs/components-charts-energysharebar--docs) · [TrendBarChart](./?path=/docs/components-charts-trendbarchart--docs) · [MonthCalendar](./?path=/docs/components-charts-monthcalendar--docs) · [StatTile](./?path=/docs/components-charts-stattile--docs) · [BarList](./?path=/docs/components-charts-barlist--docs) · [ChartLegend](./?path=/docs/components-charts-chartlegend--docs)
