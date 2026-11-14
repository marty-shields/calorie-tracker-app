# Food naming guide

How to turn a CoFID name (the `originalName`, never edited) into the everyday `name` the app shows. Example: `Potatoes, old, boiled in unsalted water, flesh only` becomes `Boiled potatoes`. Follow the rules mechanically so two people (or agents) naming neighbouring foods land on the same pattern. The seed's file format is in the [Seed README](README.md).

The guide was checked against the 2,853 foods in `cofid-foods.json`. The seed carries group codes only, so the group names below are read off the contents.

## Shape of a name

```
[Cooking or state] Food [(qualifier; qualifier)]
```

Everything is optional except the food. Raw is the default: a food with no cooking prefix is raw (or needs no cooking).

## 1. Word order

- Food last, cooking first, as people say it: `Boiled potatoes`, `Grilled cod`, `Wholemeal bread`. Never CoFID's comma inversion (`Potatoes, old, boiled`).
- Type and variety words go before the food in everyday order: `Red lentils`, `Basmati rice`, `Victoria plums`, `Cheddar cheese`. Cuts and parts stay after the animal: `Lamb best end neck cutlets`.
- Prefix is one word: the cooking method (`Boiled`, `Fried`, `Stir-fried`, `Grilled`, `Roasted`, `Baked`, `Steamed`, `Stewed`, `Braised`, `Poached`, `Barbecued`, `Casseroled`) or, when there is none, the state (`Dried`, `Canned`, `Frozen`, `Smoked`, `Uncooked`). A cooking method beats a state: `Beans, aduki, whole, dried, boiled` is `Boiled aduki beans` (add `(dried)` only if needed to tell names apart). Cooked-then-frozen or similar goes in a qualifier.
- Dishes keep their own name and drop the comma list: `Chilli, vegetable, homemade` is `Vegetable chilli`; `Pie, beef, ... retail` is `Beef pie (retail)`; `Curry, chick pea dhal` is `Chick pea dhal curry`.
- Keep CoFID's singular or plural when it reads naturally (`Potatoes`, `Aubergine`, `Bhaji`); do not respell.

## 2. What drops and what stays

| CoFID wording | In the name |
| --- | --- |
| `raw` (459 foods) | Dropped (raw is the default). `uncooked` is kept as the prefix `Uncooked` |
| `boiled`, `stewed`, `cooked` `in unsalted water` | Prefix only: `Boiled`, `Stewed`, `Cooked`. The unsalted water drops |
| `boiled in salted water`, `boiled and salted` | `Boiled ... (salted water)`, `(salted)` |
| `flesh only`, `meat only`, `old`, `average` | Dropped. They never change what the food is. Keep `new`, `young` |
| `flesh and skin`, `meat and skin`, `with skin` | `(with skin)`; `without skin` is `(skinless)` |
| `lean`, `lean only`, `lean and fat`, `fat` | `(lean)`, `(lean and fat)`; a part sold on its own is a name: `Lamb fat` |
| `weighed with bones and skin`, `weighed with stones`, ... | Kept, in the weighing slot: `(weighed with bones and skin)` |
| `fried in rapeseed oil`, `roasted in corn oil`, ... | Prefix `Fried`, `Roasted`; the fat goes in a qualifier only when the same food is cooked in more than one fat (see 5) |
| `homemade` (557 foods) | Dropped, unless the same dish also exists without a source word, then `(homemade)` (see 5) |
| `retail`, `takeaway`, `reheated` | Kept: `(retail)`, `(takeaway)`, `(reheated)` |
| `made with ...`, `with ...`, `in ...` | Kept as the qualifier: `(made with semi-skimmed milk)`, `(in syrup)`, `(in brine)` |
| `fortified`, `unfortified`, `reduced fat`, `canned in juice`, `whole contents` | Kept when they tell foods apart (`(unfortified)`); `drained` and `whole contents` drop unless a sibling needs them |
| percentages (`(26-39%)`), `Gujerati` and other proper names | Kept as written |

Do not invent or drop facts beyond this table. If a CoFID word is not listed and it distinguishes two foods, keep it as a qualifier.

## 3. Spelling

British: yoghurt (CoFID writes `Yogurt`: change it), courgette, aubergine, grey, flavour, colour, fibre, savoury. Never yogurt, eggplant, zucchini, gray, flavor, color, fiber. Keep CoFID's own spellings of foreign dishes (`dahl`, `bhaji`, `Gujerati`) unless they are plainly mistyped.

## 4. Casing, punctuation, length

- Sentence case: first letter capital, the rest lower case. Proper names keep a capital (`Cheddar`, `Victoria`, `Basmati`, `Lancashire hotpot`).
- No commas anywhere. Join with `and`, not `&`. Remove the CoFID stray quotes and backticks (`'with added fibre'` becomes `with added fibre`).
- No trailing full stop. Brackets only for qualifiers, one pair per name, items separated by `; `. `/` stays only inside a food that has alternatives in CoFID (`Pakora/bhajia`).
- At most 64 characters (CoFID's longest is 91; 94% of CoFID names are already under 60).
- No duplicate names anywhere in the seed, compared case-insensitively.

## 5. Telling near-duplicates apart: the qualifier order

Qualifiers live in one bracket, always in this order (skip empty slots):

1. **Portion or size**: `family size`, `individual`, `lean`, `lean and fat`, `with skin`, `skinless`, `extra trimmed`, `reduced fat`
2. **Fat or liquid used**: `rapeseed oil`, `sunflower oil`, `butter`, `salted water`, `in syrup`, `in juice`, `made with whole milk`
3. **Weighing basis**: `weighed with bone`, `weighed with stones`
4. **Source**: `homemade`, `retail`, `takeaway`

Rules for when a qualifier is added:

- Siblings move together. If one member of a set needs a fat, a weighing basis or a source to be unique, every member of that set gets that slot, in this order. `Fried onions (rapeseed oil)`, `Fried onions (corn oil)`, `Fried onions (sunflower oil)`, never one bare and two bracketed.
- A fat is named only when the same food is cooked in more than one fat. A single `Fried in sunflower oil` entry is just `Fried ...`.
- `homemade` is kept only when the dish also exists with no source word: `Chutney, tomato` is `Tomato chutney` and `Chutney, tomato, homemade` is `Tomato chutney (homemade)`. Counted over the data: 6 of the 542 `homemade` foods have a bare twin, and 8 have a `retail` twin, so `(retail)` is kept on the twin and `homemade` drops.
- If two names are still equal after this, take the CoFID words you dropped that differ between them (section 2 lists them) and add the earliest slot that fits. Never add numbers, codes or `(2)`.
- Two foods share an identical `originalName` (see below). Tell them apart by what the nutrients show, or ask the reviewer. Do not guess silently.

How well this works: dropping the table's words (and keeping everything else) merges 81 foods into 35 sets of equal names. 26 are fat sets (`Fried in rapeseed / sunflower / corn oil`), 6 are `homemade` twins, and the last 3 sets are the cases below. A food whose CoFID name states no fat stays bare next to its siblings that state one: `Cod, in batter, fried, takeaway` is `Fried cod in batter (takeaway)` and `Cod, in batter, fried in sunflower oil, takeaway` is `Fried cod in batter (sunflower oil; takeaway)`. The two repeated `originalName`s need a judgement call:

| `originalName` | Foods |
| --- | --- |
| `Red snapper, flesh only, fried in blended oil, weighed with bones and skin` | `16-127` (126 kcal, 24.5 g protein) and `16-128` (64 kcal, 12.5 g protein): CoFID repeats the name. The energy of the second is about half, so ask whether it is a different portion basis, and say so in the PR |
| `Beef, mince, stewed` | `18-470` (group MAC, 209 kcal) is plain stewed mince; `19-607` (group MR, 139 kcal) is a dish. Name the dish one for example `Stewed beef mince dish`, once the nutrients and the group show what it is |

## 6. Worked examples by group

Group codes and sizes are from the seed; names are what the contents show.

**DR (309, vegetable and bean dishes, mostly `homemade`).** Cooked dishes: dish name first, `homemade` drops.

| CoFID | Name |
| --- | --- |
| `Sambar, homemade` | `Sambar` |
| `Chilli, vegetable, homemade` | `Vegetable chilli` |
| `Tomatoes, stuffed with rice` | `Tomatoes stuffed with rice` |
| `Beanburger, red kidney bean, fried in rapeseed oil` | `Fried red kidney bean burger` (one fat, so no fat qualifier) |
| `Bhaji, potato, with butter ghee, homemade` | `Potato bhaji (butter ghee)` (the `with ...` is the fat slot, which three siblings share) |
| `Curry, chick pea, whole and tomato, Punjabi, with butter, homemade` | `Punjabi chick pea and tomato curry (butter)` |

**DG (229, vegetables).** `Carrots, young, raw` is `Young carrots`; `Carrots, young, boiled in unsalted water` is `Boiled young carrots`; `Okra, stir-fried in corn oil` is `Stir-fried okra (corn oil)`; `Salsify, flesh only, boiled in unsalted water` is `Boiled salsify`; `Seaweed, nori, dried, raw` is `Dried nori seaweed`; `Potatoes, old, roasted in lard` is `Roasted potatoes (lard)` (four fats exist, so every roast potato entry carries its fat).

**FA (210, fruit).** `Plums, Victoria, flesh and skin, raw` is `Victoria plums (with skin)`; `Plums, Victoria, flesh and skin, raw, weighed with stones` is `Victoria plums (with skin; weighed with stones)`; `Plums, Victoria, stewed without sugar` is `Stewed Victoria plums (no sugar)`; `Peaches, canned in juice, whole contents` is `Canned peaches (in juice)`; `Peaches, canned in syrup, whole contents` is `Canned peaches (in syrup)`; `Rambutan, flesh only` is `Rambutan`; `Apples, eating, dried` is `Dried eating apples`.

**MR (151, meat dishes).** `Pie, beef, puff or shortcrust pastry, family size, retail` is `Beef pie (family size; retail)`, with the size in slot 1 beside the portion; `Meat samosas, takeaway` is `Meat samosas (takeaway)`; `Coq au vin, homemade` is `Coq au vin`; `Cornish pasty, homemade` is `Cornish pasty`; `Chilli con carne, retail, reheated, with rice` is `Chilli con carne with rice (retail; reheated)`.

**JA (151, white fish; JC 53, other fish and shellfish).** `Cod, flesh only, poached in milk, butter and salt added` is `Poached cod (in milk with butter and salt)`; the same food `weighed with bones and skin` adds `; weighed with bones and skin`; `Cod, in batter, fried, takeaway` is `Fried cod in batter (takeaway)`; `Haddock, flesh only, grilled` is `Grilled haddock`; `Eel, conger, flesh only, raw` is `Conger eel`; `Rock Salmon/Dogfish, in batter, fried in sunflower oil` is `Fried rock salmon/dogfish in batter (sunflower oil)` (three fats exist, so all three carry it).

**MAE (105, lamb), MAC (91, beef), MAG (65, pork), MCA (64, chicken).** Cut after the animal; the lean/fat split is slot 1: `Lamb, best end neck cutlets, grilled, lean and fat, weighed with bone` is `Grilled lamb best end neck cutlets (lean and fat; weighed with bone)`; `Lamb, lean only, raw, average, extra trimmed` is `Lamb (lean; extra trimmed)`; `Lamb, fat, average, raw` is `Lamb fat`; `Beef, braising steak, braised, lean and fat` is `Braised beef braising steak (lean and fat)`; `Chicken, breast, grilled without skin, meat only` is `Grilled chicken breast (skinless)`; `Chicken, breast, grilled, meat and skin` is `Grilled chicken breast (with skin)`.

**DB (66, pulses).** `Beans, aduki, whole, dried, raw` is `Dried aduki beans`; `Beans, aduki, whole, dried, boiled in unsalted water` is `Boiled aduki beans`; `Lentils, red, split, dried, boiled in unsalted water` is `Boiled red split lentils`.

**Other groups worth a note.** BL (45, cheese): `Cheese, Cheddar, English` is `English Cheddar cheese`, `Cheese, Cheddar type, half fat` is `Half fat Cheddar-type cheese`, keep a CoFID quoted claim such as `'30% less fat'` as `30% less fat` in the bracket; GA (47, nuts): `Barcelona nuts, Kernel only` is `Barcelona nuts (kernel only)`, because the shell-weighed twin needs it; AM (42, biscuits), AN (41, cakes), AP (38, pastries), AS (48, puddings and pies): same rules as DR; Q groups (alcoholic drinks) keep the unit `Millilitre` and need no change.
