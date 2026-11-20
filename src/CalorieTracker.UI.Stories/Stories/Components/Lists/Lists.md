---
$attribute: CustomPage("Components/Lists/Overview")
---

# Lists

## `MealHeader`
The heading of a meal group: the meal `Name` in small bold capitals, its total `Calories` in kcal (left out when null, for an empty meal) and a round `+` `IconButton` labelled "Add to {Name}" that raises `OnAdd`. A long name wraps.
- `Now`: a "Now" chip after the name, for a meal whose time window is open.
- `Hint`: muted text just before the `+`, such as "from 11:00", for a meal not yet started.
- `Summary`: one line such as "3 items · 348 kcal" in place of `Calories`, for a meal folded to its header.
- `OnToggle`: when set, the name, chip, hint and total form a button with `aria-expanded` (set from `Expanded`) and a down or up chevron; when not set the header is plain text.
- `Expanded`: whether the meal below is open; only used with `OnToggle`.
- `data-testid`: the prefix `TestId` gives the header, `{TestId}-name`, `-calories`, `-summary`, `-now`, `-hint`, `-toggle` and `-add`.

Stories: With entries, Empty, Open now, Before its window, Folded, Folded and opened and Large total.

Stories: [MealHeader](./?path=/docs/components-lists-mealheader--docs)
