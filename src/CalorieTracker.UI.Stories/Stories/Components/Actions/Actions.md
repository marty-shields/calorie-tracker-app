---
$attribute: CustomPage("Components/Actions/Overview")
---

# Actions

## `AppButton`
A button with a 48px touch target.

| Variant | Use for |
|---|---|
| Primary | The main action on a screen: Save. One per screen. |
| Secondary | Other actions in the bottom bar or a card: Cancel. |
| Danger | Confirming a destructive action: Delete. Filled red. |

- `FullWidth` fills the row; bottom-bar buttons use it.
- `Disabled` dims the button with `--opacity-disabled`.
- `Busy` shows a turning ring, `--spinner-size-small` across in the text colour, beside the text, sets `aria-busy` and ignores taps. The caller sets the text, e.g. "Saving…". The ring stands still when the phone asks for reduced motion.
- `data-testid`: `TestId`.

## `Icon`
A Material Symbols Outlined icon (weight 400), drawn as an SVG and hidden from screen readers. `IconButton` uses it.

- `Name`: an `IconName`.
- `Tone`: `Default` takes the text colour; `Muted` uses `--color-text-muted`, `Success` `--color-success`, `Error` `--color-error`.
- `Size`: `Default` is `--font-size-xl`, `Small` is `--font-size-lg`.
- Chevron: the end of a tappable row, `Muted`.
- Search: a search box, `Muted`.
- Up and Down: an increase (`Success`) or decrease (`Error`).
- Edit (pencil) and Check (tick): used by `IconButton`.
- Today and BarChart: the Today and Trends items in `BottomNav`.
- Scan: the Scan barcode tile of the `AddSheet`, and Search: its Search foods tile.
- Plus and Minus: add or remove, e.g. a stepper, a meal's add button or the raised Add button of `BottomNav`.
- ChevronLeft: the previous item in a sequence, such as the previous day; Chevron points right. Back is for leaving a screen.
- Star and StarFilled: not a favourite and a favourite. The star button in the top bar of Serving; the icon takes the top bar's text colour.

## `IconButton`
A button that shows an `Icon`, with a 48px touch target.

| Icon | Use for |
|---|---|
| Settings | Opening Settings from the Today top bar. |
| Back | The back arrow in the top bar. |
| Close | Removing an item: `Muted`, `Small`. |
| Delete | Deleting something, after a confirm. |
| Check | Marking something done. |
| Edit | Opening something again to change it (`Muted`). |
| Star, StarFilled | Starring a food as a favourite (`Pressed` says which). |

- `AriaLabel` names the button.
- `Disabled` dims the button with `--opacity-disabled` and ignores taps; it keeps its place (the Trends strip's Next on the current span).
- The icon is `--color-text`; on the top bar that is `--color-on-bar`. `Muted` uses `--color-text-muted`.
- `Pressed` makes the button a toggle: `aria-pressed` is "true" or "false" (the star button). Null leaves the attribute off.
- `Active` fills the button with a round `--color-selected` background to show it is on.
- `Style`: `Plain` (the default) is the icon alone. `Soft` puts it on a lighter round `--color-chip-soft` chip with a `--color-on-chip-soft` icon; like `Filled` it ignores `Active` and `Muted` and keeps the 48px target; one `Soft Add` story. `Filled` puts it on a round `--color-chip` chip with a `--color-on-chip` icon, for the add action of a section such as `MealHeader`; it ignores `Active` and `Muted`. It keeps the 48px target, focus ring, `AriaLabel` and `TestId`. Each story except Active and Muted (which Filled ignores) has a `Filled` twin, plus `Filled Compact`.
- `Size`: `Default` is `--font-size-xl`, `Small` is `--font-size-lg`.
- `data-testid`: `TestId`.

Stories: [AppButton](./?path=/docs/components-actions-appbutton--docs), [Icon](./?path=/docs/components-actions-icon--docs), [IconButton](./?path=/docs/components-actions-iconbutton--docs)
