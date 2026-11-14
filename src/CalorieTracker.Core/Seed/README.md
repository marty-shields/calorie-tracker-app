# Food seed

`cofid-foods.json` is the generic-food seed: the foods of the UK Composition of Foods Integrated Dataset (CoFID) 2021, one food per line. It is generated from the workbook plus the everyday names in [`names/`](#everyday-names). The seed loader bundles it with the app. Names follow the [naming guide](naming-guide.md).

## Source and licence

McCance and Widdowson's Composition of Foods Integrated Dataset 2021, Public Health England (now UKHSA), Crown copyright, published under the [Open Government Licence v3.0](https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/). Source page: <https://www.gov.uk/government/publications/composition-of-foods-integrated-dataset-cofid>. The attribution is in the repo's `NOTICE`.

## Regenerate

1. Download the CoFID workbook (`.xlsx`) from the source page into a folder outside the repo. `*.xlsx` is git-ignored, so it is never committed.
2. Run `python3 -I .github/scripts/build-food-seed.py <path to cofid.xlsx> src/CalorieTracker.Core/Seed/cofid-foods.json` (`py -I` on Windows). It needs only Python 3.
3. The run prints `kept 2853 of 2887 foods` (the 2021 workbook) and a warning for each repeated food code. Every kept food has a row in the Inorganics sheet (no "has no row" warning on the 2021 workbook), so the 41 foods with `saltG` `null` are all `N` or blank sodium; a warning appears if a later workbook breaks that. A rerun on the same workbook is byte-identical.

Every food has an everyday name. Regenerating keeps them: the script reads `names/*.json` (next to the output file, or `--names <folder>`) and applies them. With no `names/` folder the output is the plain workbook names.

## Everyday names

Names live in `names/<foodGroup>.json`, one file per CoFID group code (`names/AA.json`). Each is an object of `sourceId` to everyday name, one entry per line, sorted by `sourceId` (repeated codes use the `#2` form):

```json
{
"11-001":"Arrowroot",
"11-002":"Pearl barley"
}
```

The generator sets `name` from the entry and leaves `originalName` and `sourceId` alone. It fails on a `sourceId` that is not in the workbook output, an entry in the file of another group, or an empty name. Edit `names/`, then regenerate the seed; never edit `name` in `cofid-foods.json` by hand. Names follow the [naming guide](naming-guide.md).

`python3 -I .github/scripts/check-food-names.py` checks the seed against `names/` and the guide: every food has an overlay entry, no two foods share a name (case-insensitive; an unnamed food counts with its seed name), the seed `name` equals its entry, and each name has no comma, no American spelling and at most 80 characters. Any entry counts as named, including one equal to `originalName`. `--allow-missing` lets foods with no entry pass (for a work in progress; CI runs strict, so every food needs a name). `--seed` and `--names` point it at other files. `--base <seed.json>` also fails when a food's `sourceId` set or any `originalName` differs from the base seed, when `seedVersion` is below the base's, or when any food field (such as a name) differs while `seedVersion` is not above the base's; CI runs it on every pull request against the base branch's seed (it has no workbook, so only this check guards those fields there). By hand: `git show origin/main:src/CalorieTracker.Core/Seed/cofid-foods.json > /tmp/base.json`, then `python3 -I .github/scripts/check-food-names.py --base /tmp/base.json`. A repeated `sourceId` key in an overlay file is an error in both scripts, and `--names` on the generator needs an existing folder.

## Format

`{"seedVersion":3,"source":"CoFID 2021","foods":[...]}`, one food per line, sorted by `sourceId`, `\n` line endings. `seedVersion` is a constant in the script, bumped whenever the names or content change: 3 since the everyday names (2 was the food-search change).

| Key | Meaning |
| --- | --- |
| `source` | `CoFID` |
| `sourceId` | CoFID food code, such as `11-001` (see the repeated-code rule below) |
| `name` | Everyday display name, set from `names/` |
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
- Bump `seedVersion` in `build-food-seed.py` whenever the content or names change, or installed apps will not pick the change up.
