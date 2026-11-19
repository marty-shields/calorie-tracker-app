---
$attribute: CustomPage("Components/Feedback/Overview")
---

# Feedback

## `Spinner`
A turning ring with a label under it, for work in progress. The ring is `--color-border` with a `--color-accent` arc, `--spinner-size` across, one turn every `--spinner-duration`; it stands still when the phone asks for reduced motion. The label is muted text. `role="status"` (a live region).
- `data-testid`: `TestId`.

## `LoadFailed`
A load error with **Try again**, in place of the content that didn't load. The message is bold `--color-error` text in a `--color-error` box (`role="alert"`); Try again is a Primary full-width `AppButton` under it. Focus moves to Try again when it shows. A failed focus logs Warning 3301 and the error stays.
- `data-testid`: `{TestId}-error` on the message, `{TestId}-retry` on Try again.

## `Snackbar` and `SnackbarService`
A short message with an optional action (Undo), shown through `AppScreen`'s `Status` slot: put `<Snackbar />` there. The bar is a `--color-inverse-surface` box (`--color-on-inverse-surface` text, `--shadow-raised`) with the action as bold `--color-inverse-primary` text in a `--touch-target` button; it renders nothing while no message is posted. The slot is the polite live region (`role="status"`, `aria-live="polite"`), so a message shown later is read out, and the action is a native button the keyboard reaches.
- `SnackbarService` (a singleton) holds the one message, apart from any screen, so a message posted before a screen change shows on the next screen. `Show(text, actionLabel, onAction, duration)` replaces any message showing and restarts the timer; the default duration is 5 seconds (`SnackbarService.DefaultDuration`, timed with `TimeProvider`). Running the action dismisses the message first, so the action fires once. `Dismiss()` removes it. The action shows only when both its label and handler are given.
- `Service`: a service of its own to show; the app's when unset (the stories give each story one, with no timeout).
- `data-testid`: `snackbar`, `snackbar-message`, `snackbar-action`.

Stories: [Spinner](./?path=/docs/components-feedback-spinner--docs) · [LoadFailed](./?path=/docs/components-feedback-loadfailed--docs) · [Snackbar](./?path=/docs/components-feedback-snackbar--docs)
