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
- Home: the Dashboard item in `BottomNav`.
- Scan: the Scan item in `BottomNav`.
- Plus and Minus: add or remove, e.g. a stepper or a meal's add button.
- ChevronLeft: the previous item in a sequence, such as the previous day; Chevron points right. Back is for leaving a screen.

## `IconButton`
A button that shows an `Icon`, with a 48px touch target.

| Icon | Use for |
|---|---|
| Settings | Opening Settings from the Dashboard top bar. |
| Back | The back arrow in the top bar. |
| Close | Removing an item: `Muted`, `Small`. |
| Delete | Deleting something, after a confirm. |
| Check | Marking something done. |
| Edit | Opening something again to change it (`Muted`). |

- `AriaLabel` names the button.
- The icon is `--color-text`; on the top bar that is `--color-on-bar`. `Muted` uses `--color-text-muted`.
- `Active` fills the button with a round `--color-selected` background to show it is on.
- `Size`: `Default` is `--font-size-xl`, `Small` is `--font-size-lg`.
- `data-testid`: `TestId`.

Stories: [AppButton](./?path=/docs/components-actions-appbutton--docs), [Icon](./?path=/docs/components-actions-icon--docs), [IconButton](./?path=/docs/components-actions-iconbutton--docs)
