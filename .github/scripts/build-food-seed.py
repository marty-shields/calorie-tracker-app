#!/usr/bin/env python3
"""Usage: python3 -I build-food-seed.py <cofid.xlsx> <out.json> [--names <folder>]
Builds the generic-food seed file from the CoFID 2021 workbook (src/CalorieTracker.Core/Seed/README.md says where to get it).
Reads sheets "1.3 Proximates" and "1.4 Inorganics", joined on (food code, food name). Columns are found by the tag in row 2
of each sheet, never by position; the food code is column A, the name B and the group D. Uses only the standard library.
Everyday names: each <folder>/<foodGroup>.json (default: the "names" folder beside <out.json>, which may be absent; a --names folder must exist) is
{"11-001":"Arrowroot",...}, one entry per line, sorted by sourceId. It sets that food's "name"; originalName and sourceId stay as
the workbook has them. Exits 1 on an entry whose sourceId is not in the output, whose food is in another group than the file name,
or whose name is empty, and on a repeated sourceId key in a file. Names are stripped of surrounding spaces once.
Types: each food gets a "type" (Ingredient, Food, Dish or Drink) from its CoFID group (GROUP_TYPES), then NAME_RULES may move it by its everyday name and TYPE_OVERRIDES by its sourceId (an override whose sourceId is not in the output exits 1).
Exits 1 on a food whose group has no type. Seed/README.md holds the table as the decision record.
Writes {"seedVersion", "source", "foods"} with one food per line, sorted by sourceId, so a rerun is byte-identical.
A food with no value (N or blank) for energy, protein, carbohydrate or fat is dropped; the other values become null.
Prints kept/dropped counts, any repeated food code and any kept food with no row in 1.4 to stderr. Exits 1 on a missing sheet, tag or column, or a value it
cannot read."""
import decimal
import glob
import json
import os
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from decimal import Decimal

# Bumped by hand when the seed's content is revised (for example by the everyday-names edit or the food types).
SEED_VERSION = 5
SOURCE_NAME = "CoFID 2021"
SOURCE = "CoFID"
PROXIMATES = "1.3 Proximates"
INORGANICS = "1.4 Inorganics"
TAG_ROW = 2
FIRST_DATA_ROW = 4
# 1.3 tag -> (output key, required). NSP (ENGFIB) is deliberately not read: the app's fibre is AOAC.
PROXIMATE_TAGS = {
    "KCALS": ("energyKcal", True),
    "PROT": ("proteinG", True),
    "CHO": ("carbsG", True),
    "FAT": ("fatG", True),
    "AOACFIB": ("fibreG", False),
    "TOTSUG": ("sugarsG", False),
    "SATFOD": ("saturatesG", False),
}
SODIUM_TAG = "NA"
# CoFID group codes of alcoholic drinks all start with Q (beer, cider, wine, spirits, liqueurs, alcohol-free lager included).
ALCOHOL_GROUP_PREFIX = "Q"
INGREDIENT, FOOD, DISH, DRINK = "Ingredient", "Food", "Dish", "Drink"
# CoFID group code -> food type. Every group in the workbook needs an entry.
GROUP_TYPES = {
    **dict.fromkeys(
        "A AA AC AD AF AG AO BJC BJS BL BNE BTM CA D DA DAE DAM DB DF DG DI F FA G GA H J JA JC JK JM MAA MAC MAE MAG MAI "
        "MCA MCC MCE MCG MCI MCK MCM MCO MEA MEC MEE MG OA OB OC OE OF S SC WY".split(), INGREDIENT),
    **dict.fromkeys(
        "AI AK AM AN AP BJP BN BNH BNS BP CDH DAP DAR JR MBG MI SEA SEC SN SNA SNC WA WAC WAE WC WCG WCN WE".split(), FOOD),
    **dict.fromkeys("AB AE AS AT BR BV CD CDE DR MIG MR WAA WCD".split(), DISH),
    **dict.fromkeys(
        "BA BAB BAE BAH BAK BAR BC BH FC P PA PAA PAC PCA PCC PE Q QA QC QE QF QG QI QK".split(), DRINK),
}
COATED = r"(?<!un)coated|crumbs|batter|breaded"
# (CoFID groups, pattern searched case-insensitively in the everyday name, type it gives), applied after the group's type.
# The first rule of the food's group that matches wins, so a narrower rule goes before a wider one.
NAME_RULES = [
    ("A", r"pizza|made up with", DISH),
    ("AC", r"takeaway|fried rice|pilau|biryani|^cooked savoury rice", DISH),
    ("AC", r"savoury rice", FOOD),
    ("AD", r"filled|fried|stir|takeaway|macaroni cheese", DISH),
    ("AD", r"^canned|sauce mixes", FOOD),
    ("AF", r"buns|garlic and herb|papadum", FOOD),
    ("AG", r"croissant|pain au|bagel", FOOD),
    ("AT", r"polenta", INGREDIENT),
    ("BAR", r"evaporated|condensed|dried|powder", INGREDIENT),
    ("BL", r"cheese spread|processed", FOOD),
    ("BV", r"dried cheese sauce|packet mix", FOOD),
    ("DA", r"shaped|frozen", FOOD),
    ("DAM", r"mashed", DISH),
    ("DB", r"^mixed dahl", DISH),
    ("DB", r"baked beans|re-fried|houmous", FOOD),
    ("DF", r"mushy", FOOD),
    ("DG", r"pickled", FOOD),
    ("F FA", r"^(stewed|baked cooking apples)(?!.*no sugar)|fruit salad", DISH),
    ("F FA", r"^canned|olives", FOOD),
    ("G GA", r"(?<!un)salted|roasted|trail mix|raisins|peanut butter", FOOD),
    ("J", r"curry", DISH),
    ("JA", r"in milk", DISH),
    ("JC", r"pickled|in tomato sauce", FOOD),
    ("JM", r"in white wine sauce", DISH),
    ("J JA JC JK JM", COATED + r"|goujon", FOOD),
    ("MCA", COATED + r"|nugget|kiev", FOOD),
    ("MG", r"in milk", DISH),
    ("S SC", r"\bjam\b|marmalade|lemon curd|chocolate (nut )?spread|topping", FOOD),
    ("BN", r"lassi", DRINK),
    ("WY", r"distilled water", DRINK),
    ("WY", r"gravy.*made up", DISH),
    ("WY", r"gravy|^(smooth|wholegrain) mustard|mustard powder \(made up\)|extract", FOOD),
    ("PAA", r"soup powder", FOOD),
]
# sourceId -> (type, why): single foods no name rule captures cleanly. Applied after NAME_RULES; an id that is not in the
# workbook output fails the build, so a renumbered food cannot silently lose its type.
TYPE_OVERRIDES = {
    "13-607": (DRINK, "carrot juice is a drink"),
    "18-042": (FOOD, "mince patties are a processed burger product"),
    "18-216": (FOOD, "pork crackling is a ready-to-eat snack"),
    "18-490": (FOOD, "crispy Chinese style duck is a prepared product"),
    "19-496": (FOOD, "sliced ham is a processed meat product (bacon and gammon stay Ingredients)"),
}
NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
REL_NS = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
PKG_REL_NS = "{http://schemas.openxmlformats.org/package/2006/relationships}"
CENT = Decimal("0.01")
NOISE = Decimal("0.0000000001")


def fail(message):
    sys.exit(f"build-food-seed: {message}")


def column_of(ref):
    return re.match(r"[A-Z]+", ref).group(0)


def shared_strings(book):
    if "xl/sharedStrings.xml" not in book.namelist():
        return []
    root = ET.fromstring(book.read("xl/sharedStrings.xml"))
    return ["".join(t.text or "" for t in si.iter(NS + "t")) for si in root.iter(NS + "si")]


def sheet_path(book, name):
    sheets = ET.fromstring(book.read("xl/workbook.xml")).find(NS + "sheets")
    sheet = next((s for s in sheets if s.get("name") == name), None)
    if sheet is None:
        fail(f"sheet '{name}' is not in the workbook")
    rels = ET.fromstring(book.read("xl/_rels/workbook.xml.rels"))
    target = next((r.get("Target") for r in rels.iter(PKG_REL_NS + "Relationship") if r.get("Id") == sheet.get(REL_NS + "id")), None)
    if target is None:
        fail(f"sheet '{name}' has no relationship entry")
    return target.lstrip("/") if target.startswith("/") else "xl/" + target


def read_sheet(book, strings, name):
    """The sheet as {row number: {column letter: text}}; blank cells are absent."""
    rows = {}
    for row in ET.fromstring(book.read(sheet_path(book, name))).iter(NS + "row"):
        cells = {}
        for cell in row.iter(NS + "c"):
            kind = cell.get("t")
            if kind == "inlineStr":
                text = "".join(t.text or "" for t in cell.iter(NS + "t"))
            else:
                v = cell.find(NS + "v")
                if v is None or v.text is None:
                    continue
                text = strings[int(v.text)] if kind == "s" else v.text
            cells[column_of(cell.get("r"))] = text
        rows[int(row.get("r"))] = cells
    return rows


def tag_columns(rows, sheet, tags):
    found = {}
    for column, tag in rows.get(TAG_ROW, {}).items():
        found.setdefault(tag.strip(), []).append(column)
    columns = {}
    for tag in tags:
        if tag not in found:
            fail(f"tag {tag} is not in row {TAG_ROW} of '{sheet}'")
        if len(found[tag]) > 1:
            fail(f"tag {tag} is in more than one column of '{sheet}'")
        columns[tag] = found[tag][0]
    return columns


def two_places(value):
    """Rounds half up to 2 dp, after removing float noise such as 4.5999999999999996 or 4.4999999999999998E-2."""
    return value.quantize(NOISE, rounding=decimal.ROUND_HALF_UP).quantize(CENT, rounding=decimal.ROUND_HALF_UP)


def parse_value(text, where):
    """None for N or blank (no value), 0 for Tr, else the Decimal."""
    text = (text or "").strip()
    if text in ("", "N"):
        return None
    if text == "Tr":
        return Decimal(0)
    try:
        value = Decimal(text)
    except decimal.InvalidOperation:
        fail(f"{where}: cannot read '{text}' as a number")
    if not value.is_finite() or value < 0:
        fail(f"{where}: '{text}' is not a non-negative number")
    return value


def number_text(value):
    return format(two_places(value).normalize(), "f")


def food_rows(rows, sheet):
    """(row number, code, name, cells) of each data row that has a food code."""
    for number in sorted(n for n in rows if n >= FIRST_DATA_ROW):
        cells = rows[number]
        code = cells.get("A", "").strip()
        if not code:
            continue
        name = cells.get("B", "").strip()
        if not name:
            fail(f"'{sheet}' row {number}: food {code} has no name")
        yield number, code, name, cells


def check_headers(rows, sheet, expected):
    """expected is (column, allowed titles) pairs; 1.4 leaves the A title blank, so "" is allowed there."""
    head = rows.get(1, {})
    for column, titles in expected:
        if head.get(column, "").strip() not in titles:
            fail(f"'{sheet}' column {column} is '{head.get(column, '').strip()}', expected '{titles[-1]}'")


def sodium_by_food(rows):
    column = tag_columns(rows, INORGANICS, [SODIUM_TAG])[SODIUM_TAG]
    sodium = {}
    for number, code, name, cells in food_rows(rows, INORGANICS):
        if (code, name) in sodium:
            fail(f"'{INORGANICS}' row {number}: food {code} '{name}' is listed twice")
        sodium[(code, name)] = parse_value(cells.get(column), f"'{INORGANICS}' row {number} {SODIUM_TAG}")
    return sodium


def build(path):
    try:
        book = zipfile.ZipFile(path)
    except (OSError, zipfile.BadZipFile) as error:
        fail(f"cannot open {path}: {error}")
    with book:
        strings = shared_strings(book)
        proximates = read_sheet(book, strings, PROXIMATES)
        inorganics = read_sheet(book, strings, INORGANICS)
    check_headers(proximates, PROXIMATES, (("A", ("Food Code",)), ("B", ("Food Name",)), ("D", ("Group",))))
    check_headers(inorganics, INORGANICS, (("A", ("", "Food Code")), ("B", ("Food Name",))))
    columns = tag_columns(proximates, PROXIMATES, PROXIMATE_TAGS)
    sodium = sodium_by_food(inorganics)

    foods, seen_keys, occurrences, total = [], set(), {}, 0
    for number, code, name, cells in food_rows(proximates, PROXIMATES):
        total += 1
        if (code, name) in seen_keys:
            fail(f"'{PROXIMATES}' row {number}: food {code} '{name}' is listed twice")
        seen_keys.add((code, name))
        occurrence = occurrences[code] = occurrences.get(code, 0) + 1
        source_id = code if occurrence == 1 else f"{code}#{occurrence}"
        if occurrence > 1:
            print(f"warning: food code {code} is repeated; '{name}' (row {number}) gets sourceId {source_id}", file=sys.stderr)

        values, dropped = {}, False
        for tag, (key, required) in PROXIMATE_TAGS.items():
            value = parse_value(cells.get(columns[tag]), f"'{PROXIMATES}' row {number} {tag}")
            if value is None and required:
                dropped = True
            values[key] = value
        if dropped:
            continue
        if (code, name) not in sodium:
            print(f"warning: food {code} '{name}' (row {number}) has no row in '{INORGANICS}'; its saltG is null", file=sys.stderr)
        na = sodium.get((code, name))
        salt = None if na is None else na.quantize(NOISE, rounding=decimal.ROUND_HALF_UP) * Decimal("2.5") / 1000
        values["saltG"] = salt
        group = cells.get("D", "").strip()
        foods.append((code, occurrence, {
            "source": SOURCE,
            "sourceId": source_id,
            "name": name,
            "originalName": name,
            "foodGroup": group,
            "unit": "Millilitre" if group.startswith(ALCOHOL_GROUP_PREFIX) else "Gram",
            "type": None,
            **{key: None if value is None else Raw(number_text(value)) for key, value in values.items()},
        }))
    foods.sort(key=lambda f: (f[0], f[1]))
    print(f"kept {len(foods)} of {total} foods, dropped {total - len(foods)}", file=sys.stderr)
    return [food for _, _, food in foods]


def no_repeated_keys(pairs):
    keys = [key for key, _ in pairs]
    repeated = sorted({key for key in keys if keys.count(key) > 1})
    if repeated:
        raise ValueError(f"repeated key {', '.join(repeated)}")
    return dict(pairs)


def apply_names(foods, folder, required):
    """Sets each food's name from <folder>/<foodGroup>.json; a missing folder changes nothing unless it is required."""
    if not os.path.isdir(folder):
        if required:
            fail(f"names folder {folder} does not exist")
        return
    by_id = {food["sourceId"]: food for food in foods}
    for path in sorted(glob.glob(os.path.join(folder, "*.json"))):
        group = os.path.splitext(os.path.basename(path))[0]
        try:
            with open(path, encoding="utf-8") as file:
                names = json.load(file, object_pairs_hook=no_repeated_keys)
        except (OSError, ValueError) as error:
            fail(f"cannot read {path}: {error}")
        if not isinstance(names, dict):
            fail(f"{path}: expected an object of sourceId to name")
        for source_id, name in names.items():
            food = by_id.get(source_id)
            if food is None:
                fail(f"{path}: {source_id} is not a food in the workbook output")
            if food["foodGroup"] != group:
                fail(f"{path}: {source_id} is in group {food['foodGroup']}, not {group}")
            name = name.strip() if isinstance(name, str) else ""
            if not name:
                fail(f"{path}: {source_id} has an empty name")
            food["name"] = name


def food_type(group, name):
    """The group's type, moved by the group's name rule when the name matches; None for a group with no type."""
    found = GROUP_TYPES.get(group)
    if found is None:
        return None
    for groups, pattern, becomes in NAME_RULES:
        if group in groups.split() and re.search(pattern, name, re.IGNORECASE):
            return becomes
    return found


def apply_types(foods):
    """Sets each food's type from its group, everyday name and override; run after apply_names."""
    known = {food["sourceId"] for food in foods}
    for source_id in TYPE_OVERRIDES:
        if source_id not in known:
            fail(f"TYPE_OVERRIDES: {source_id} is not a food in the workbook output")
    for food in foods:
        found = food_type(food["foodGroup"], food["name"])
        if found is None:
            fail(f"food {food['sourceId']} is in group {food['foodGroup']}, which has no type in GROUP_TYPES")
        food["type"] = TYPE_OVERRIDES.get(food["sourceId"], (found,))[0]


class Raw(str):
    """A number already formatted as JSON text."""


def encode(value):
    if value is None:
        return "null"
    if isinstance(value, Raw):
        return str(value)
    return json.dumps(value, ensure_ascii=False)


def render(foods):
    lines = ["{" + ",".join(f'"{k}":{encode(v)}' for k, v in food.items()) + "}" for food in foods]
    head = f'{{"seedVersion":{SEED_VERSION},"source":{encode(SOURCE_NAME)},"foods":['
    return head + "\n" + ",\n".join(lines) + "\n]}\n"


def main(argv):
    args = argv[1:]
    names = None
    if len(args) == 4 and args[2] == "--names":
        names = args[3]
        args = args[:2]
    if len(args) != 2:
        sys.exit(__doc__)
    foods = build(args[0])
    apply_names(foods, names or os.path.join(os.path.dirname(os.path.abspath(args[1])), "names"), names is not None)
    apply_types(foods)
    text = render(foods)
    with open(args[1], "w", encoding="utf-8", newline="\n") as out:
        out.write(text)


if __name__ == "__main__":
    main(sys.argv)
