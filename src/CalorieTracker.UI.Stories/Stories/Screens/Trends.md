---
$attribute: CustomPage("Screens/Trends/Overview")
---

# Trends

How a week, a month or a year went, from the bar's **Trends** item. View: `TrendsView`; page: `TrendsPage`. Route: `/trends?period=week|month|year&from=yyyy-MM-dd`, where `from` is any day of the span; without a query it opens on the current week. All figures come from `TrendsService`; a day is on target within 10% either side of the target it had.

| Part | Content |
|---|---|
| Top bar | Title "Trends"; Settings gear on the right. No back arrow; Android back goes to Today, the start destination (`NavDestinations.Start`). |
| Period control | `SegmentedControl` Week, Month, Year. Switching opens the span of that kind that holds today (or, when another span is showing, the day it starts on). |
| Period stepper | The kit's `PeriodStepper`: previous and next arrows round the label (a `span`, not a heading, so the heading order is the screen title, then each card's title): "9 – 15 Nov" (a week, Monday to Sunday; "30 Dec 2026 – 5 Jan 2027" across years), "November 2026", "2026". The arrows move a week, month or year. Next is dimmed and does nothing on the current span, so it stops there; past spans have no limit. |
| Bar | The kit's `BottomNav` with **Trends** current, to the right of the raised **Add**. Add opens the same `AddSheet` as on Today, through `AddSheetHost` (shared with Today), and always adds to today. |

## Week
| Part | Content |
|---|---|
| Tiles | Each tile is the kit's `StatTile`, label first. **Average**: "2,124" with "kcal a day, 24 over target" (or "under" or "on target"). **On target**: "3 of 7" with "days within 10% of target" (7 is the days of the week). |
| Calories by day | The kit's `ChartLegend` with dots (Under, On target, Over with a ▲ marker, "Target 2,100, band ±10%" on the dashed-line swatch, since the kit has no band swatch) (legend test ids `trends-legend-0`, `-1`, ... run in this order: Week with a target Under, On target, Over, Target; Week without a target Under, On target, Over; Month Under, On target, Over, Not logged, Target; Year Under, On target, Over, Target) and a `TrendBarChart`: one bar for each past logged day, coloured by its status against its own target, an over marker on Over days and the tallest value labelled. Today and later days, and days not logged, are empty slots. A bar is a button: it opens that day on Today (`/?date=yyyy-MM-dd`). |
| Average macros | `EnergyShareBar` with the average protein, carbs and fat in grams. |
| Kcal by meal | `BarList` of the average kcal in Breakfast, Lunch, Dinner and Snacks. |

## Month
| Part | Content |
|---|---|
| Tiles | **Average** ("kcal a day"); **On target** ("N of M", "logged days within 10% of target"); **Streak** ("13 days", "logged in a row"; another month is labelled **Longest streak** and shows its longest run); **Logged** ("N of M", "past days logged"). |
| Each day | The `MonthCalendar`: a cell for each day coloured by status, an over marker, today outlined, later days disabled. A cell is a button that opens that day on Today. |
| Average a day, by week | A `TrendBarChart` of Monday-start weeks (the first and last cover only their days in the month, but are named and opened by their Monday), labelled W1, W2, … A bar opens that week in Week. |
| Top foods | `BarList` of the 5 foods with the most kcal, as `TrendsService` ranks them. |

## Year
| Part | Content |
|---|---|
| Tiles | **Average** ("kcal a day"); **On target** ("N of M", "months averaging within 10% of target"; M is the months judged against a target; a month of fewer than 3 logged days is left out here and from the lowest month); **Logged** ("88%", "283 of 321 days"); **Lowest month** ("Oct", "1,990 kcal a day"). |
| Average a day, by month | A `TrendBarChart` against the target band, one bar for each month with a logged day, labelled by its short month name (Jan, Mar). A bar opens that month in Month. |
| Days logged per month | `BarList` of "28 of 31" for each month, full at 31 days. |

## Thin buckets
A week or month averages from as few as one logged day. Such a bucket (fewer than 3 logged days, `TrendsService.MinDays`) is still drawn, but faded (`--opacity-thin`), its text alternative ends "based on 2 days", and a line under the chart says "Faded bars rest on fewer than 3 logged days."

## Little data
Fewer than 3 logged past days in the span keep the layout, even none at all when the person logged on other days. A tile whose figure rests on too few days shows a dash and "Not enough data yet" under its caption (the kit's own text); the macros, meal and top foods cards say "Not enough data yet" in place of the figure. The chart and calendar still show the days that are logged.

## Other states
| State | Content |
|---|---|
| Loading | The control and stepper stay, with a spinner "Loading…". |
| Failed | The control and stepper stay, with "Couldn't load your trends" and **Try again**. |
| No data | The control and stepper stay, with `EmptyState` "Nothing logged yet" and "Your trends show here once you've logged a day". Shown only when no day before today was ever logged, in the span or out of it (`TrendsReport.AnyLoggedBeforeToday`). A span with no logged past day, for a person who logged on other days, keeps the layout with "Not enough data yet" (see Little data). |

Every bar and calendar day is a button (the charts get `OnSelect`), so a keyboard reaches them with Tab and opens one with Enter. The page reads again when an entry changes, such as a usual food added from the sheet. It logs the span, the number of days and what was opened (day, week or month) only (`UiEvent` 2400 block).

Dark theme: the same layout; switch the theme in the toolbar to see it.

States: [Week](./?path=/story/screens-trends--week), [Month](./?path=/story/screens-trends--month), [Year](./?path=/story/screens-trends--year), [Loading](./?path=/story/screens-trends--loading), [Failed](./?path=/story/screens-trends--failed), [No data](./?path=/story/screens-trends--no-data), [Little data](./?path=/story/screens-trends--little-data)

`data-testid`: `trends-period` (`-week`, `-month`, `-year`), `trends` (the stepper), `trends-label`, `trends-previous`, `trends-next`, `trends-average`, `trends-on-target`, `trends-streak`, `trends-logged`, `trends-lowest` (each with `-label`, `-value`, `-caption`, `-note`; a tile has no `-change`), `trends-days`, `trends-chart` (`-bar-<date>`), `trends-macros`, `trends-share`, `trends-meals`, `trends-meal-list`, `trends-calendar-card`, `trends-calendar` (`-day-<date>`), `trends-weeks`, `trends-weeks-chart`, `trends-months`, `trends-months-chart` (`-bar-<date>`), `trends-top`, `trends-top-list`, `trends-logged-card`, `trends-logged-list`, `trends-thin-note`, `trends-loading`, `trends-load-failed` (`-error`, `-retry`), `trends-empty`, `bottom-nav`, `trends-link`, `add-button`, `settings`.
