# Food seed

`cofid-foods.json` is the generic-food seed: the foods of the UK Composition of Foods Integrated Dataset (CoFID) 2021, one food per line. It is generated from the workbook plus the everyday names in [`names/`](#everyday-names). The seed loader bundles it with the app. Names follow the [naming guide](naming-guide.md).

## Source and licence

McCance and Widdowson's Composition of Foods Integrated Dataset 2021, Public Health England (now UKHSA), Crown copyright, published under the [Open Government Licence v3.0](https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/). Source page: <https://www.gov.uk/government/publications/composition-of-foods-integrated-dataset-cofid>. The attribution is in the repo's `NOTICE`.

## Regenerate

1. Download the CoFID workbook (`.xlsx`) from the source page into a folder outside the repo. `*.xlsx` is git-ignored, so it is never committed.
2. Run `python3 -I .github/scripts/build-food-seed.py <path to cofid.xlsx> src/CalorieTracker.Core/Seed/cofid-foods.json` (`py -I` on Windows). It needs only Python 3.
3. The run prints `kept 2853 of 2887 foods` and then `dropped 679 Dishes, kept 2174 foods` (the 2021 workbook) and a warning for each repeated food code. Every kept food has a row in the Inorganics sheet (no "has no row" warning on the 2021 workbook), so the 26 foods with `saltG` `null` are all `N` or blank sodium; a warning appears if a later workbook breaks that. A rerun on the same workbook is byte-identical.

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

`{"seedVersion":8,"source":"CoFID 2021","foods":[...]}`, one food per line, sorted by `sourceId`, `\n` line endings. `seedVersion` is a constant in the script, bumped whenever the names or content change. History:

- 8: undiluted cordial and squash moved to millilitres.
- 7: liquids moved to millilitres.
- 6: the Dishes were dropped.
- 5: the second food-type review.
- 4: the first food types.
- 3: the everyday names.
- 2: the food-search change.

| Key | Meaning |
| --- | --- |
| `source` | `CoFID` |
| `sourceId` | CoFID food code, such as `11-001` (see the repeated-code rule below) |
| `name` | Everyday display name, set from `names/` |
| `originalName` | CoFID food name, never edited; stored on the food as `OriginalName` |
| `foodGroup` | CoFID group code, such as `AA`; stored on the food as `FoodGroup` |
| `unit` | `Millilitre` for alcoholic drinks (group code starts with `Q`, alcohol-free lager included) and for liquids, else `Gram`. Liquids are chosen by the constants `LIQUID_GROUPS`, `LIQUID_NAME_RULES`, `LIQUID_IDS`, `DRY_NAME` and `DRY_IDS` in `build-food-seed.py` (see their comments); a food that matches `DRY_NAME` (powder not made up, dried, evaporated, condensed, concentrate, a juice weighed as whole fruit) stays `Gram`, as do all other groups. Undiluted cordial and squash are poured, so they are `Millilitre`. Schema step 11 (`FoodUnitUpgrade`) sets the stored CoFID foods in its frozen list of seed 7 millilitre ids to `Millilitre` on upgrade, and schema step 12 (`FoodCordialUnitUpgrade`) does the same for its frozen list of the seven undiluted cordial and squash ids that seed 8 added (step 11 had already shipped, so its list is unchanged); a diary entry keeps its amount and its calories (they scale by amount / 100 whatever the unit) |
| `type` | `Ingredient`, `Food` or `Drink`, from the group and the name rules in [Food types](#food-types) |
| `energyKcal`, `proteinG`, `carbsG`, `fatG` | Per 100 g or 100 ml; a food with no value for any of these is left out |
| `fibreG` | AOAC fibre (not NSP); `null` when not measured |
| `sugarsG`, `saturatesG` | Total sugars and saturated fatty acids per 100 g; `null` when not measured |
| `saltG` | Sodium (mg) × 2.5 ÷ 1000, from the Inorganics sheet; `null` when not measured |

Values are rounded half up to 2 decimal places; `Tr` (trace) is 0; `N` or blank is `null`.

## Food types

Search does not filter by `type` and nothing shows it (the type chips are gone); it stays stored as a hint for later. Search ranks by the everyday `name` and the CoFID `originalName` (a plain common food such as "Milk, whole, average" ranks first for a one-word query) and shows each food in the Common group; see the Food search screen page. The generator gives each food its group's type, then a name rule or a single-food override may move it. The owner approved this table (issue #33); change it in `build-food-seed.py` (`GROUP_TYPES`, `NAME_RULES`, `TYPE_OVERRIDES`), bump `SEED_VERSION` and regenerate. A group with no entry fails the build. Open Food Facts foods get their type when the barcode is looked up, from the product data alone: when `categories_tags` has any tag, `Drink` only if it contains `en:beverages` (case-insensitive), otherwise `Food`, so oils, vinegars and soy sauce sold in ml are `Food`; the `ml` unit makes it `Drink` only when `categories_tags` is missing, empty or malformed. They are never `Ingredient`, since the product data does not say. **Dishes are not in the seed.** The generator still works out a Dish from the group and the name rules below, then drops it (seed 6; the 679 Dish values were generic estimates and the noisiest results), so the seed holds 2,174 foods: Ingredient 1,472, Food 546, Drink 156. The 30 foods that only a name rule made Dishes lost their everyday name with the overlay entry, so `TYPE_OVERRIDES` marks them Dish for the generator. Schema step 4 (`DropDishesUpgrade`) deletes the stored Dish rows and renumbers Drink from 3 to 2.

| Type | Groups |
| --- | --- |
| Ingredient | A, AA, AC, AD, AF, AG, AO, BJC, BJS, BL, BNE, BTM, CA, D, DA, DAE, DAM, DB, DF, DG, DI, F, FA, G, GA, H, J, JA, JC, JK, JM, MAA, MAC, MAE, MAG, MAI, MCA, MCC, MCE, MCG, MCI, MCK, MCM, MCO, MEA, MEC, MEE, MG, OA, OB, OC, OE, OF, S, SC, WY |
| Food | AI, AK, AM, AN, AP, BJP, BN, BNH, BNS, BP, CDH, DAP, DAR, JR, MBG, MI, SEA, SEC, SN, SNA, SNC, WA, WAC, WAE, WC, WCG, WCN, WE |
| Dish (dropped) | AB, AE, AS, AT, BR, BV, CD, CDE, DR, MIG, MR, WAA, WCD |
| Drink | BA, BAB, BAE, BAH, BAK, BAR, BC, BH, FC, P, PA, PAA, PAC, PCA, PCC, PE, Q, QA, QC, QE, QF, QG, QI, QK |

Name rules (case-insensitive, searched in the everyday `name`, applied after the group's type; the first rule of the food's group that matches wins; 168 foods move, 60 of them by the first approved rules and 108 by the review of the Ingredients below):

| Group | Name contains | Becomes | Moves |
| --- | --- | --- | --- |
| A | pizza, made up with | Dish | 2 |
| AC | takeaway, fried rice, pilau, biryani, cooked savoury rice (starts with) | Dish | 4 |
| AC | savoury rice (the uncooked packet) | Food | 1 |
| AD | filled, fried, stir, takeaway, macaroni cheese | Dish | 7 |
| AD | canned (starts with), sauce mixes | Food | 3 |
| AF | buns, garlic and herb, papadum | Food | 3 |
| AG | croissant, pain au, bagel | Food | 1 |
| AT | polenta | Ingredient | 1 |
| BAR | evaporated, condensed, dried, powder | Ingredient | 5 |
| BL | cheese spread, processed | Food | 5 |
| BV | dried cheese sauce, packet mix | Food | 1 |
| DA | shaped, frozen | Food | 1 |
| DAM | mashed | Dish | 2 |
| DB | mixed dahl (starts with) | Dish | 1 |
| DB | baked beans, re-fried, houmous | Food | 6 |
| DF | mushy | Food | 1 |
| DG | pickled | Food | 3 |
| F, FA | stewed or baked cooking apples (starts with) unless "no sugar"; fruit salad | Dish | 29 |
| F, FA | canned (starts with), olives | Food | 14 |
| G, GA | salted (not unsalted), roasted, trail mix, raisins, peanut butter | Food | 10 |
| J | curry | Dish | 2 |
| JA | in milk | Dish | 2 |
| JC | pickled, in tomato sauce | Food | 2 |
| JM | in white wine sauce | Dish | 1 |
| J, JA, JC, JK, JM | coated (not uncoated), crumbs, batter, breaded, goujon | Food | 36 |
| MCA | coated (not uncoated), crumbs, batter, breaded, nugget, kiev | Food | 5 |
| MG | in milk | Dish | 1 |
| S, SC | jam, marmalade, lemon curd, chocolate spread, chocolate nut spread, topping | Food | 9 |
| BN | lassi | Drink | 1 |
| WY | distilled water | Drink | 1 |
| WY | gravy ... made up | Dish | 1 |
| WY | gravy, smooth or wholegrain mustard, mustard powder (made up), extract | Food | 6 |
| PAA | soup powder | Food | 1 |

Overrides (`TYPE_OVERRIDES`, by `sourceId`, for single foods no name rule captures cleanly; the build fails on an id that is not in the output): 13-607 Carrot juice (Drink), 18-042 Barbecued beef mince patties (Food), 18-216 Cooked pork crackling (Food), 18-490 Crispy Chinese style duck (Food), 19-496 Ham (Food; bacon and gammon stay Ingredients).

The Ingredient review (owner request: foods made up with milk or similar are Dishes; check each Ingredient for Food or Dish): an Ingredient is a single component people cook with (raw or plainly cooked meat, fish, vegetables and fruit, flours, grains, pulses, fats, sugars, herbs, spices, eggs, plain cheese, breads). Plain boiled rice, pasta, beans, potatoes and vegetables stay Ingredients. A food that combines components (porridge made up with milk, fruit stewed or baked with sugar, mashed potato with fat, fish cooked in milk or a sauce, curries, macaroni cheese) is a Dish. A ready-to-eat processed product (canned fruit and baked beans, pickles, jam, spreads, mustard, seasoned nuts, processed cheese, coated fish) is a Food. Unfortified porridge oats (11-788) stay an Ingredient and Cooked porridge oats made up with semi-skimmed milk (11-789) is a Dish.

Judgment calls: BAR milk products stay Ingredients, "Plain pilau rice" stays a Dish, sandwiches are Dishes, breads Ingredients, homemade sauces and soups Dishes (retail ones Foods), yoghurt and ice cream Foods, JR fish products Foods, cereals Foods, milkshakes Drinks, and drink mixes, squash, cordial and juice concentrates Drinks. Every stored food has a type (the column is not null). Schema step 3 (`FoodTypeUpgrade`) gave the foods stored before types existed one: an Open Food Facts food measured in millilitres is `Drink` (no categories are stored, so the unit is all there is), any other food (CoFID rows too) is `Food`; the CoFID rows then get the seed's type on the seed 4 load. Step 4 later removed the Dish type (see above).

## Repeated food codes

CoFID reuses code `13-669` for two foods ("Aubergine, flesh and skin, roasted in rapeseed oil" and "Watercress, raw"). Both are kept and joined to the Inorganics sheet by code and name. Numbering follows sheet order across every row of the sheet, dropped foods included, so ids do not shift when a food is dropped: the first in sheet order keeps the plain code even if it is dropped (then no food has the plain code), and each later one gets `#2`, `#3` and so on (`13-669#2` is the watercress), so `(source, sourceId)` stays unique.

## Loading

The file is an embedded resource of `CalorieTracker.Core` (`cofid-foods.json`). `ISeedLoader.LoadAsync` runs after the database initializer on each start, without holding up the first screen, and loads the foods in chunks of 500 through `FoodStore`.

- The `SeedVersion` table holds the loaded `seedVersion` for each source. It is written only after the last chunk, so a crash part way retries on the next start (the upsert is idempotent).
- A stored version equal to or higher than the bundled one loads nothing. A missing or lower one loads every food and then stores the bundled version.
- Foods match on `source` and `sourceId`; an existing row keeps its `Id`. Rows of other sources are never read or written.
- After the upsert, CoFID foods (and their `Barcode` rows, if any) whose `sourceId` is not in the seed are deleted, in one transaction before the version is stored. This is how a database whose Dish rows were not typed Dish (schema step 4 only deletes those) loses them. Rows of other sources are untouched.
- `originalName` and `foodGroup` are stored on the Food row (`OriginalName`, `FoodGroup`) and are required for every seed food: a food missing either fails the parse. Open Food Facts foods leave both null. Schema step 9 (`FoodOriginalNameUpgrade`) adds the columns and deletes the stored CoFID seed version, so an installed app reloads the seed once on its next start and fills them; food ids and diary entries are unchanged.
- Bump `seedVersion` in `build-food-seed.py` whenever the content or names change, or installed apps will not pick the change up.
