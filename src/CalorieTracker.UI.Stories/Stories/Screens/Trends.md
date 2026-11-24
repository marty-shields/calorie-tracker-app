---
$attribute: CustomPage(StoryTitles.Prefix + "Screens/Trends/Overview")
---

# Trends

How a week, a month or a year went, from the bar's **Trends** item. View: `TrendsView`; page: `TrendsPage`. Route: `/trends?period=week|month|year&from=yyyy-MM-dd`, where `from` is any day of the span; without a query it opens on the current week. All figures come from `TrendsService`; a day is on target within 10% either side of the target it had.

`SegmentedControl`, `PeriodStepper`, `BottomNav`, `StatTile`, `ChartLegend` and `EmptyState` here are components of the `Shields.UI` kit, not this app's; their stories are under Components on the [combined stories site](https://marty-shields.github.io/app-ui-kit/).

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
| Calories by day | The kit's `ChartLegend` with dots (Under, On target, Over, "Target 2,100" on the dashed-line swatch and "Band ±10%" on the band swatch) (legend test ids `trends-legend-0`, `-1`, ... run in this order: Week with a target Under, On target, Over, Target, Band; Week without a target Under, On target, Over; Month Under, On target (a ✓ box), Over, Not logged, Target, Band; Year Under, On target, Over, Target, Band) and the kit's `TrendBarChart` (the target is a dashed line and the band's two edges are outlines in `--color-chart-band`): one bar for each past logged day, coloured by its status against its own target, Over shown by colour alone (no marker) and the tallest value labelled. Today and later days, and days not logged, are empty slots. A bar is a button: it opens that day on Today (`/?date=yyyy-MM-dd`). |
| Average macros | The kit's `StackedBar` with the average protein, carbs and fat in the data series 1, 2 and 3, sized by calories and labelled in grams (test ids `trends-share-segment-<n>` and `-label-<n>`). |
| Kcal by meal | The kit's `BarList` of the average kcal in Breakfast, Lunch, Dinner and Snacks. |

## Month
| Part | Content |
|---|---|
| Tiles | **Average** ("kcal a day"); **On target** ("N of M", "logged days within 10% of target"); **Streak** ("13 days", "logged in a row"; another month is labelled **Longest streak** and shows its longest run); **Logged** ("N of M", "past days logged"). |
| Each day | The kit's `MonthCalendar` (Monday first, headings M T W T F S S): a cell for each day coloured by status, a ✓ on on-target days (over days by colour alone), today with a double ring, later days disabled. A cell is a button that opens that day on Today. |
| Average a day, by week | The kit's `TrendBarChart` of Monday-start weeks (the first and last cover only their days in the month, but are named and opened by their Monday), labelled W1, W2, … A bar opens that week in Week. |
| Top foods | The kit's `BarList` of the 5 foods with the most kcal, as `TrendsService` ranks them. |

## Year
| Part | Content |
|---|---|
| Tiles | **Average** ("kcal a day"); **On target** ("N of M", "months averaging within 10% of target"; M is the months judged against a target; a month of fewer than 3 logged days is left out here and from the lowest month); **Logged** ("88%", "283 of 321 days"); **Lowest month** ("Oct", "1,990 kcal a day"). |
| Average a day, by month | The kit's `TrendBarChart` against the target band, one bar for each month with a logged day, labelled by its short month name (Jan, Mar). A bar opens that month in Month. |
| Days logged per month | The kit's `BarList` of "28 of 31" for each month, full at 31 days. |

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

States: [Week](./?path=/story/calorie-tracker-screens-trends--week), [Month](./?path=/story/calorie-tracker-screens-trends--month), [Year](./?path=/story/calorie-tracker-screens-trends--year), [Loading](./?path=/story/calorie-tracker-screens-trends--loading), [Failed](./?path=/story/calorie-tracker-screens-trends--failed), [No data](./?path=/story/calorie-tracker-screens-trends--no-data), [Little data](./?path=/story/calorie-tracker-screens-trends--little-data)

`data-testid`: `trends-period` (`-week`, `-month`, `-year`), `trends` (the stepper), `trends-label`, `trends-previous`, `trends-next`, `trends-average`, `trends-on-target`, `trends-streak`, `trends-logged`, `trends-lowest` (each with `-label`, `-value`, `-caption`, `-note`; a tile has no `-change`), `trends-days`, `trends-chart` (`-bar-<date>`), `trends-macros`, `trends-share`, `trends-meals`, `trends-meal-list`, `trends-calendar-card`, `trends-calendar` (`-day-<date>`), `trends-weeks`, `trends-weeks-chart`, `trends-months`, `trends-months-chart` (`-bar-<date>`), `trends-top`, `trends-top-list`, `trends-logged-card`, `trends-logged-list`, `trends-thin-note`, `trends-loading`, `trends-load-failed` (`-error`, `-retry`), `trends-empty`, `bottom-nav`, `trends-link`, `add-button`, `settings`.
