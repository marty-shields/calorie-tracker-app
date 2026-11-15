---
$attribute: CustomPage("Components/Inputs/Overview")
---

# Inputs

## `SegmentedControl`
A full-width row of 2–4 segments; one is chosen and filled with `--color-selected`. For choosing between a few views or ranges, and for the Theme in Settings. Each segment is a radio with a 48px touch target; `Label` names the group for screen readers only. An option's `AriaLabel`, when set, is what screen readers read instead of its label, e.g. *Last month* for *1M*. When `Value` matches no option, no segment is chosen.
- `data-testid`: `TestId` on the control; each option's `TestId` on its radio.

## `FilterChips`
A row of rounded chips that wraps onto more lines when it must; one is chosen and filled with `--color-selected`. For the food search's Ingredients, Foods, Drinks and All. Each chip is a radio with a 48px touch target; `Label` names the group for screen readers only.
- `data-testid`: `TestId` on the group; each option's `TestId` on its radio.

Stories: [SegmentedControl](./?path=/docs/components-inputs-segmentedcontrol--docs) · [FilterChips](./?path=/docs/components-inputs-filterchips--docs)
