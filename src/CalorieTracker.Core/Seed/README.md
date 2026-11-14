# Food seed

`cofid-foods.json` is the generic-food seed: the foods of the UK Composition of Foods Integrated Dataset (CoFID) 2021, one food per line. It is generated, then edited by hand for everyday names (the `name` field only). The seed loader bundles it with the app. Names follow the [naming guide](naming-guide.md).

## Source and licence

McCance and Widdowson's Composition of Foods Integrated Dataset 2021, Public Health England (now UKHSA), Crown copyright, published under the [Open Government Licence v3.0](https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/). Source page: <https://www.gov.uk/government/publications/composition-of-foods-integrated-dataset-cofid>. The attribution is in the repo's `NOTICE`.

## Regenerate

1. Download the CoFID workbook (`.xlsx`) from the source page into a folder outside the repo. `*.xlsx` is git-ignored, so it is never committed.
2. Run `python3 -I .github/scripts/build-food-seed.py <path to cofid.xlsx> src/CalorieTracker.Core/Seed/cofid-foods.json` (`py -I` on Windows). It needs only Python 3.
3. The run prints `kept 2853 of 2887 foods` (the 2021 workbook) and a warning for each repeated food code. Every kept food has a row in the Inorganics sheet (no "has no row" warning on the 2021 workbook), so the 41 foods with `saltG` `null` are all `N` or blank sodium; a warning appears if a later workbook breaks that. A rerun on the same workbook is byte-identical.

Regenerating overwrites hand-edited names, so redo the everyday-names edit (or merge the diff) afterwards.

## Format

`{"seedVersion":1,"source":"CoFID 2021","foods":[...]}`, one food per line, sorted by `sourceId`, `\n` line endings. `seedVersion` is a constant in the script, bumped when the content is revised.

| Key | Meaning |
| --- | --- |
| `source` | `CoFID` |
| `sourceId` | CoFID food code, such as `11-001` (see the repeated-code rule below) |
| `name` | Display name; starts equal to `originalName` and is the field edited for everyday names |
| `originalName` | CoFID food name, never edited |
| `foodGroup` | CoFID group code, such as `AA` |
| `unit` | `Millilitre` when the group code starts with `Q` (alcoholic drinks, alcohol-free lager included), else `Gram` |
| `energyKcal`, `proteinG`, `carbsG`, `fatG` | Per 100 g or 100 ml; a food with no value for any of these is left out |
| `fibreG` | AOAC fibre (not NSP); `null` when not measured |
| `sugarsG`, `saturatesG` | Total sugars and saturated fatty acids per 100 g; `null` when not measured |
| `saltG` | Sodium (mg) × 2.5 ÷ 1000, from the Inorganics sheet; `null` when not measured |

Values are rounded half up to 2 decimal places; `Tr` (trace) is 0; `N` or blank is `null`.

## Repeated food codes

CoFID reuses code `13-669` for two foods ("Aubergine, flesh and skin, roasted in rapeseed oil" and "Watercress, raw"). Both are kept and joined to the Inorganics sheet by code and name. Numbering follows sheet order across every row of the sheet, dropped foods included, so ids do not shift when a food is dropped: the first in sheet order keeps the plain code even if it is dropped (then no food has the plain code), and each later one gets `#2`, `#3` and so on (`13-669#2` is the watercress), so `(source, sourceId)` stays unique.

## Loading

The file is an embedded resource of `CalorieTracker.Core` (`cofid-foods.json`). `ISeedLoader.LoadAsync` runs after the database initializer on each start, without holding up the first screen, and loads the foods in chunks of 500 through `FoodStore`.

- The `SeedVersion` table holds the loaded `seedVersion` for each source. It is written only after the last chunk, so a crash part way retries on the next start (the upsert is idempotent).
- A stored version equal to or higher than the bundled one loads nothing. A missing or lower one loads every food and then stores the bundled version.
- Foods match on `source` and `sourceId`; an existing row keeps its `Id`. Rows of other sources are never read or written.
- Foods missing from a newer seed are left in place, never deleted.
- `originalName` and `foodGroup` are not stored (the Food table has no columns for them).
- Bump `seedVersion` in `build-food-seed.py` whenever the content or names change, including the everyday-names edit, or installed apps will not pick the change up.
