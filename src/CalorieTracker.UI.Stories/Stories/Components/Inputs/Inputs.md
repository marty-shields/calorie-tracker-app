---
$attribute: CustomPage("Components/Inputs/Overview")
---

# Inputs

## `SegmentedControl`
A full-width row of 2–4 segments; one is chosen and filled with `--color-selected`. For choosing between a few views or ranges, and for the Theme in Settings. Each segment is a radio with a 48px touch target; `Label` names the group for screen readers only. An option's `AriaLabel`, when set, is what screen readers read instead of its label, e.g. *Last month* for *1M*. When `Value` matches no option, no segment is chosen.
- `data-testid`: `TestId` on the control; each option's `TestId` on its radio.

## `FilterChips`
A row of rounded chips that wraps onto more lines when it must; one is chosen and filled with `--color-selected`. For the food search's Ingredients, Foods, Dishes, Drinks and All. Each chip is a radio with a 48px touch target; `Label` names the group for screen readers only.
- `data-testid`: `TestId` on the group; each option's `TestId` on its radio.

## `NumberField`
A numeric input for amounts and the daily target. `Unit` (e.g. *g*, *kcal*) sits after the number. `Stepper` adds − and + buttons that move the number by `Step`; they never go below `Minimum`, and − is off once the number reaches it. Typing is not clamped: the field raises `ValueChanged` on every keystroke with the typed number (null when the text isn't one) and the parent decides whether it is valid. A phone shows the numeric keypad. `InvalidMessage` shows an error under the field in the error tone (`--color-error`, a thicker border) and is announced to screen readers (`role="alert"`, `aria-invalid`, `aria-describedby`). `Disabled` greys out the input and both buttons. `AutoFocus` puts the cursor in the field when it first appears. `Label` names the field for screen readers only. The input and each button have a 48px touch target.
- `data-testid`: `TestId` on the input; the buttons, unit and message add `-minus`, `-plus`, `-unit` and `-error`.

## `DateStrip`
A previous button, a label and a next button on one row, for moving between days. The label reads *Today, 15 Nov*, *Yesterday, 14 Nov*, *Tomorrow, 16 Nov* or the weekday and date (with the year when it is not this year). Tapping the label raises `OnToday` from any day. There is no limit in either direction. The caller passes `Date` and `Today`, so the strip never reads the clock; the parent changes `Date` in the callbacks. Every target is 48px: the buttons are named *Previous day* and *Next day*, the label *<label>, go to today*.
- `data-testid`: `TestId` (default `date-strip`) on the strip, with `-previous`, `-label` and `-next` on its buttons.

Stories: [SegmentedControl](./?path=/docs/components-inputs-segmentedcontrol--docs) · [FilterChips](./?path=/docs/components-inputs-filterchips--docs) · [NumberField](./?path=/docs/components-inputs-numberfield--docs) · [DateStrip](./?path=/docs/components-inputs-datestrip--docs)
