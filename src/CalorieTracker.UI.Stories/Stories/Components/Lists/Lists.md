---
$attribute: CustomPage("Components/Lists/Overview")
---

# Lists

## `EmptyState`
A message in a dashed box, for a list or section with nothing in it. An optional `Hint` under it, smaller, says how to fill it.
- `data-testid`: `TestId`; the message `{TestId}-message`, the hint `{TestId}-hint`.

Stories: [EmptyState](./?path=/docs/components-lists-emptystate--docs)

## `FoodValues`
A food's values block: the name (the food's title) as a heading, a muted generic name, brand line and pack (each when given), "Per 100 g" or "Per 100 ml" and the rows Energy, Protein, Carbs and Fat, then Fibre, Sugars, Saturates and Salt only for the values given. Each row is a muted label with its bold value, divided by a `--color-border` line. Content passed in shows under the rows (the Scan screen puts its warning there). Numbers show with at most one decimal.
- `data-testid`: the prefix `TestId` gives `{TestId}-name`, `-generic`, `-brand`, `-pack`, `-per`, `-kcal`, `-protein`, `-carbs`, `-fat`, `-fibre`, `-sugars`, `-saturates`, `-salt`.

Stories: [FoodValues](./?path=/docs/components-lists-foodvalues--docs)

## `ListRow`
One row of a list: `Text`, an optional muted `Detail` line under it, and an optional muted `Aside` at the right. With `OnClick` it is a button with a chevron; without, a plain row.
- `data-testid`: `TestId`.

Stories: [ListRow](./?path=/docs/components-lists-listrow--docs)

## `MealHeader`
The heading of a meal group: the meal `Name` in small bold capitals, its total `Calories` in kcal (left out when null, for an empty meal) and a round `+` `IconButton` labelled "Add to {Name}" that raises `OnAdd`. A long name wraps. Stories: with entries, empty and a large total.
- `data-testid`: the prefix `TestId` gives the header, `{TestId}-name`, `-calories` and `-add`.

Stories: [MealHeader](./?path=/docs/components-lists-mealheader--docs)
