#!/usr/bin/env bash
# build-food-seed.py against a small CoFID-shaped workbook that this test builds in a temp folder (no binary is committed).
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib.sh"
real_script="$here/../build-food-seed.py"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# The script under test is a copy that also types group AQ (not a CoFID group), so a group that merely contains a Q can be Gram.
mkdir "$work/bin"
script="$work/bin/build-food-seed.py"
# It also swaps TYPE_OVERRIDES for a given table (the real one names real CoFID codes, so the real rows are tested on the real seed).
with_overrides() { sed -e 's/^GROUP_TYPES = {$/GROUP_TYPES = {"AQ": "Ingredient",/' \
    -e "s/^TYPE_OVERRIDES = {\$/TYPE_OVERRIDES = {$2} if True else {/" "$real_script" > "$1"; }
with_overrides "$script" ""
check "the test copy of the script types AQ" "1" "$(grep -c '^GROUP_TYPES = {"AQ": "Ingredient",$' "$script")"

# The workbook generator: mkbook.py <out.xlsx> <variant>, variant ok | rules | nosheet | notag | dupkey | dupinorganics | notnumber | negative | badgroup | types | unknowngroup | duptag | noname | badinorganics | notzip.
# A str that looks like a number is written as a number (<v>, verbatim, so float noise survives); N and Tr are shared strings.
cat > "$work/mkbook.py" <<'PY'
import re
import sys
import zipfile
from xml.sax.saxutils import escape

out, variant = sys.argv[1], sys.argv[2]
strings = []


class Inline(str):
    """Written as t="inlineStr" instead of a shared string."""


def cell(ref, value):
    if value is None:
        return ""
    if isinstance(value, Inline):
        return f'<c r="{ref}" t="inlineStr"><is><t>{escape(value)}</t></is></c>'
    if isinstance(value, str) and re.fullmatch(r"-?[0-9.]+(E-?[0-9]+)?", value):
        return f'<c r="{ref}"><v>{value}</v></c>'
    if isinstance(value, str):
        if value not in strings:
            strings.append(value)
        return f'<c r="{ref}" t="s"><v>{strings.index(value)}</v></c>'
    return f'<c r="{ref}"><v>{value}</v></c>'


def sheet(rows):
    body = ""
    for number, values in enumerate(rows, 1):
        cells = "".join(cell(f"{col}{number}", v) for col, v in values.items())
        body += f'<row r="{number}">{cells}</row>'
    return ('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>'
            + body + "</sheetData></worksheet>")


TAGS = {"J": "PROT", "K": "FAT", "L": "CHO", "M": "KCALS", "Q": "TOTSUG", "Y": "ENGFIB", "Z": "AOACFIB", "AB": "SATFOD"}
if variant == "notag":
    del TAGS["Z"]
if variant == "duptag":
    TAGS["AC"] = "FAT"
if variant == "notzip":
    with open(out, "w") as f:
        f.write("not a zip")
    sys.exit(0)


def proximate(code, name, group, kcal, prot, cho, fat, sug=None, nsp=None, fibre=None, sat=None):
    return {"A": code, "B": name, "D": group, "J": prot, "K": fat, "L": cho, "M": kcal,
            "Q": sug, "Y": nsp, "Z": fibre, "AB": sat}


foods = [
    proximate("17-001", "Lager, 4%", "QA", 29, 0.3, 1.5, 0),
    proximate("17-002", Inline("Whisky"), "QK", 222, "Tr", "Tr", 0.0),
    proximate("10-001", "Bread, white", "AA", 100, "7.0000000000000007E-2", "4.5999999999999996", 0.125,
              sug="Tr", nsp=9, fibre=2.5, sat=0.5),
    proximate("10-002", "No energy", "AA", "N", 1, 1, 1),
    proximate("10-003", "No protein", "AA", 1, None, 1, 1),
    proximate("10-004", "No carbs", "AA", 1, 1, "N", 1),
    proximate("10-005", "No fat", "AA", 1, 1, 1, None),
    proximate("10-006", "Nothing optional", "AA", 5, 1, 1, 1, sug="N", fibre="N"),
    proximate("10-007", "Trace sodium", "AA", 5, 1, 1, 1, sug="4.4999999999999998E-2"),
    proximate("10-008", "Not in inorganics", "AQ", 5, 1, 1, 1),
    proximate("13-669", "Aubergine, roasted", "DG", 62, 2.1, 5.2, 3.8),
    proximate("13-669", "Watercress, raw", "DG", 10, 1.9, "Tr", 0.3),
    proximate("13-669", "Third with the code", "DG", 7, 1, 1, 1),
    proximate("18-001", "Alcohol-free", "QA", 7, 0, 1, 0),
]
sodium = {"17-001": "1.9999999999999999", "17-002": "Tr", "10-001": 400, "10-002": 1, "10-003": 1, "10-004": 1, "10-005": 1,
          "10-006": "N", "10-007": "Tr", "13-669": 2, "18-001": 100}
inorganics = [{"A": " ", "B": "Food Name", "H": "Na"}, {"H": "NA"}, {}]
for f in foods:
    key = f["A"]
    if key == "13-669":
        value = {"Aubergine, roasted": 2, "Watercress, raw": 37, "Third with the code": None}[f["B"]]
    elif key in sodium:
        value = sodium[key]
    else:
        continue
    inorganics.append({"A": key, "B": f["B"], "H": value})
if variant == "types":
    foods += [proximate("20-001", "Pizza, cheese", "A", 250, 10, 30, 10),
              proximate("20-002", "Chips, Coated", "JA", 200, 2, 25, 10),
              proximate("20-003", "Chips, uncoated", "JA", 200, 2, 25, 10),
              proximate("20-004", "Milk, DRIED", "BAR", 400, 30, 40, 1),
              proximate("20-005", "Milk, whole", "BA", 60, 3, 5, 3),
              proximate("20-006", "Garlic bread", "AB", 300, 8, 40, 12)]
if variant == "rules":
    with open(out.rsplit("/", 1)[0] + "/rules.txt") as f:
        for number, line in enumerate(f, 1):
            group, name, _ = line.rstrip("\n").split("|")
            foods.append(proximate(f"30-{number:03d}", name, group, 100, 1, 1, 1))
if variant == "unknowngroup":
    foods.append(proximate("20-009", "Mystery", "ZZ", 1, 1, 1, 1))
if variant == "dupkey":
    foods.append(dict(foods[0]))
if variant == "dupinorganics":
    inorganics.append(dict(inorganics[3]))
if variant == "notnumber":
    foods[2]["M"] = "12 kcal"
if variant == "negative":
    foods[2]["M"] = -1
if variant == "noname":
    foods[0]["B"] = None
if variant == "badinorganics":
    inorganics[0]["B"] = "Name"

head = {"A": "Food Code", "B": "Food Name", "D": "Group"}
if variant == "badgroup":
    head["D"] = "Grp"
tag_row = dict(TAGS)
if variant == "notag":
    foods = [{k: v for k, v in f.items() if k != "Z"} for f in foods]
proximates = [head, tag_row, {}] + foods

sheets = [("1.3 Proximates", proximates), ("1.4 Inorganics", inorganics)]
if variant == "nosheet":
    sheets = sheets[:1]
bodies = [sheet(rows) for _, rows in sheets]
with zipfile.ZipFile(out, "w") as z:
    entries = "".join(f'<sheet name="{n}" sheetId="{i}" r:id="rId{i}"/>' for i, (n, _) in enumerate(sheets, 1))
    z.writestr("xl/workbook.xml", '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
               'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>'
               + entries + "</sheets></workbook>")
    rels = "".join(f'<Relationship Id="rId{i}" Target="worksheets/sheet{i}.xml"/>' for i in range(1, len(sheets) + 1))
    z.writestr("xl/_rels/workbook.xml.rels", '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
               + rels + "</Relationships>")
    for i, body in enumerate(bodies, 1):
        z.writestr(f"xl/worksheets/sheet{i}.xml", body)
    items = "".join(f"<si><t>{escape(s)}</t></si>" for s in strings)
    z.writestr("xl/sharedStrings.xml", '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' + items + "</sst>")
PY

# seed <variant>: builds the workbook, runs the script into $work/out.json; prints stderr and the exit code
seed() {
  python3 -I "$work/mkbook.py" "$work/book.xlsx" "$1" || return 1
  rm -f "$work/out.json"
  python3 -I "$script" "$work/book.xlsx" "$work/out.json" 2>&1; echo "exit=$?"
}

check "the summary, a warning for each repeat of a code and for a food missing from 1.4, and the exit code" "warning: food 10-008 'Not in inorganics' (row 13) has no row in '1.4 Inorganics'; its saltG is null
warning: food code 13-669 is repeated; 'Watercress, raw' (row 15) gets sourceId 13-669#2
warning: food code 13-669 is repeated; 'Third with the code' (row 16) gets sourceId 13-669#3
kept 10 of 14 foods, dropped 4
exit=0" "$(seed ok)"

# Sorted by code then repeat number (not sheet order); Tr is 0; 0.125 rounds half up; float noise is gone; the NSP column
# (9 for Bread) is not the fibre; Na 400 mg is 1 g salt; Na 2/37 mg are 0.01/0.09 g (half up); each repeat of a code takes the
# sodium of its own name; salt is null for N or a missing Na and for a food absent from 1.4.
check "the seed file" '{"seedVersion":6,"source":"CoFID 2021","foods":[
{"source":"CoFID","sourceId":"10-001","name":"Bread, white","originalName":"Bread, white","foodGroup":"AA","unit":"Gram","type":"Ingredient","energyKcal":100,"proteinG":0.07,"carbsG":4.6,"fatG":0.13,"fibreG":2.5,"sugarsG":0,"saturatesG":0.5,"saltG":1},
{"source":"CoFID","sourceId":"10-006","name":"Nothing optional","originalName":"Nothing optional","foodGroup":"AA","unit":"Gram","type":"Ingredient","energyKcal":5,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":null},
{"source":"CoFID","sourceId":"10-007","name":"Trace sodium","originalName":"Trace sodium","foodGroup":"AA","unit":"Gram","type":"Ingredient","energyKcal":5,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":0.05,"saturatesG":null,"saltG":0},
{"source":"CoFID","sourceId":"10-008","name":"Not in inorganics","originalName":"Not in inorganics","foodGroup":"AQ","unit":"Gram","type":"Ingredient","energyKcal":5,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":null},
{"source":"CoFID","sourceId":"13-669","name":"Aubergine, roasted","originalName":"Aubergine, roasted","foodGroup":"DG","unit":"Gram","type":"Ingredient","energyKcal":62,"proteinG":2.1,"carbsG":5.2,"fatG":3.8,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.01},
{"source":"CoFID","sourceId":"13-669#2","name":"Watercress, raw","originalName":"Watercress, raw","foodGroup":"DG","unit":"Gram","type":"Ingredient","energyKcal":10,"proteinG":1.9,"carbsG":0,"fatG":0.3,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.09},
{"source":"CoFID","sourceId":"13-669#3","name":"Third with the code","originalName":"Third with the code","foodGroup":"DG","unit":"Gram","type":"Ingredient","energyKcal":7,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":null},
{"source":"CoFID","sourceId":"17-001","name":"Lager, 4%","originalName":"Lager, 4%","foodGroup":"QA","unit":"Millilitre","type":"Drink","energyKcal":29,"proteinG":0.3,"carbsG":1.5,"fatG":0,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.01},
{"source":"CoFID","sourceId":"17-002","name":"Whisky","originalName":"Whisky","foodGroup":"QK","unit":"Millilitre","type":"Drink","energyKcal":222,"proteinG":0,"carbsG":0,"fatG":0,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0},
{"source":"CoFID","sourceId":"18-001","name":"Alcohol-free","originalName":"Alcohol-free","foodGroup":"QA","unit":"Millilitre","type":"Drink","energyKcal":7,"proteinG":0,"carbsG":1,"fatG":0,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.25}
]}' "$(cat "$work/out.json")"

check "a food with an N or blank energy, protein, carbs or fat is dropped" "" "$(grep -E '10-00[2-5]' "$work/out.json")"
check "the group must start with Q to be a Millilitre food (AQ is not)" "3" "$(grep -c '"unit":"Millilitre"' "$work/out.json")"

# The type is the group's, moved by the group's name rule (case-insensitive; "uncoated" is not "coated").
seed types > /dev/null
check "a food's type comes from its group, then the group's name rule" '10-001 Ingredient
20-002 Food
20-003 Ingredient
20-004 Ingredient
20-005 Drink' "$(python3 -I -c '
import json
import sys
foods = {f["sourceId"]: f for f in json.load(open(sys.argv[1]))["foods"]}
for key in ("10-001", "20-002", "20-003", "20-004", "20-005"):
    print(key, foods[key]["type"])
' "$work/out.json")"
check "a Dish by name rule (20-001) or by group (20-006) is dropped, and no food is a Dish" "0 0" "$(grep -c -E '"sourceId":"20-00[16]"' "$work/out.json" | tr '\n' ' ')$(grep -c '"type":"Dish"' "$work/out.json")"
# A type override beats the name rule (20-001 is a Pizza, so a Dish by rule) and the group.
with_overrides "$work/bin/override.py" '"20-001": ("Drink", "test"), "10-001": ("Food", "test")'
python3 -I "$work/mkbook.py" "$work/book.xlsx" types
python3 -I "$work/bin/override.py" "$work/book.xlsx" "$work/override.json" 2> /dev/null
check "a type override sets the type over the group and the name rule" "10-001 Food
20-001 Drink
20-002 Food" "$(python3 -I -c '
import json
import sys
foods = {f["sourceId"]: f for f in json.load(open(sys.argv[1]))["foods"]}
for key in ("10-001", "20-001", "20-002"):
    print(key, foods[key]["type"])
' "$work/override.json")"
with_overrides "$work/bin/missing.py" '"99-999": ("Food", "test")'
check "an override whose sourceId is not in the workbook fails" "build-food-seed: TYPE_OVERRIDES: 99-999 is not a food in the workbook output
exit=1" "$(python3 -I "$work/bin/missing.py" "$work/book.xlsx" "$work/missing.json" 2>&1 | tail -1; echo "exit=${PIPESTATUS[0]}")"
check "a group with no type fails" "build-food-seed: food 20-009 is in group ZZ, which has no type in GROUP_TYPES
exit=1" "$(seed unknowngroup | tail -2)"

# One row per alternative of every NAME_RULES entry: group|name|expected type. The rest of the group is its base type, so
# each row moves the food away from it ("uncoated" rows stay in the base type).
cat > "$work/rules.txt" <<'RULES'
A|Pizza, cheese|Dish
A|Porridge, made up with milk|Dish
A|Porridge oats, unfortified|Ingredient
AC|Takeaway curry|Dish
AC|Egg fried rice|Dish
AC|Pilau rice|Dish
AC|Chicken biryani|Dish
AC|Cooked savoury rice|Dish
AC|Uncooked savoury rice|Food
AC|Boiled white rice|Ingredient
AD|Pastry, filled|Dish
AD|Pastry, fried|Dish
AD|Vegetables, stir fried|Dish
AD|Pastry takeaway|Dish
AD|Macaroni cheese|Dish
AD|Canned spaghetti|Food
AD|Pasta and sauce mixes|Food
AD|Boiled pasta|Ingredient
AF|Currant buns|Food
AF|Garlic and herb bread|Food
AF|Papadums|Food
AF|White bread|Ingredient
AG|Croissant|Food
AG|Pain au chocolat|Food
AG|Bagel|Food
AT|Polenta|Ingredient
BAR|Milk, evaporated|Ingredient
BAR|Milk, condensed|Ingredient
BAR|Milk, dried|Ingredient
BAR|Milk powder|Ingredient
BL|Cheese spread|Food
BL|Cheese, processed|Food
BL|Cheese, spreadable|Ingredient
BL|Cheddar cheese|Ingredient
BV|Dried cheese sauce|Food
BV|Sauce, packet mix|Food
DA|Chips, shaped|Food
DA|Peas, frozen|Food
DAM|Potatoes, mashed|Dish
DAM|Potatoes, boiled|Ingredient
DB|Mixed dahl|Dish
DB|Dried mung bean dahl|Ingredient
DB|Canned baked beans|Food
DB|Re-fried beans|Food
DB|Houmous|Food
DB|Boiled red kidney beans|Ingredient
DF|Canned mushy peas|Food
DF|Canned peas|Ingredient
DG|Pickled onions|Food
DG|Onions, boiled|Ingredient
F|Stewed pears (with sugar)|Dish
F|Stewed pears (no sugar)|Ingredient
F|Canned pears|Food
FA|Stewed apricots|Dish
FA|Baked cooking apples|Dish
FA|Fruit salad|Dish
FA|Stewed apricots (no sugar)|Ingredient
FA|Baked cooking apples (no sugar)|Ingredient
FA|Canned peaches (in juice)|Food
FA|Green olives|Food
FA|Fresh peaches|Ingredient
G|Salted macadamia nuts|Food
GA|Roasted peanuts|Food
GA|Cashew nuts, salted|Food
GA|Trail mix|Food
GA|Peanuts and raisins|Food
GA|Smooth peanut butter|Food
GA|Peanuts, unsalted|Ingredient
GA|Walnuts|Ingredient
J|Prawn curry|Dish
J|Cod in batter|Food
J|Salmon, raw|Ingredient
JA|Cod poached in milk|Dish
JA|Chips, coated|Food
JA|Fish, crumbs|Food
JA|Fish, batter|Food
JA|Fish, breaded|Food
JA|Sole goujons|Food
JA|Chips, uncoated|Ingredient
JC|Herring, pickled|Food
JC|Sardines in tomato sauce|Food
JC|Fish, batter|Food
JC|Sardines in brine|Ingredient
JK|Chips, coated|Food
JK|Fish, crumbs|Food
JK|Fish, batter|Food
JK|Fish, breaded|Food
JM|Mussels in white wine sauce|Dish
JM|Calamari, batter|Food
JM|Mussels, raw|Ingredient
MCA|Chicken, coated|Food
MCA|Chicken, crumbs|Food
MCA|Chicken, batter|Food
MCA|Chicken, breaded|Food
MCA|Chicken nuggets|Food
MCA|Chicken kiev|Food
MCA|Chicken, uncoated|Ingredient
MG|Tripe stewed in milk|Dish
MG|Liver, raw|Ingredient
S|Lemon curd, homemade|Food
SC|Fruit jam|Food
SC|Marmalade|Food
SC|Lemon curd|Food
SC|Chocolate spread|Food
SC|Chocolate nut spread|Food
SC|Ice cream sauce topping|Food
SC|Golden syrup|Ingredient
BN|Lassi, sweet|Drink
WY|Distilled water|Drink
WY|Gravy granules, made up with water|Dish
WY|Gravy granules|Food
WY|Smooth mustard|Food
WY|Wholegrain mustard|Food
WY|Mustard powder (made up)|Food
WY|Yeast extract|Food
WY|Beef extract|Food
WY|Mustard powder|Ingredient
WY|Salt|Ingredient
PAA|Soup powder|Food
RULES
seed rules > /dev/null
check "every alternative of every name rule sets its type, and a Dish is dropped" "$(cut -d'|' -f3 "$work/rules.txt" | grep -v '^Dish$')" "$(python3 -I -c '
import json
import sys
foods = json.load(open(sys.argv[1]))["foods"]
for f in foods:
    if f["sourceId"].startswith("30-"):
        print(f["type"])
' "$work/out.json")"
seed ok > /dev/null

cp "$work/out.json" "$work/first.json"
seed ok > /dev/null
check "a second run gives identical bytes" "same" "$(cmp -s "$work/first.json" "$work/out.json" && echo same || echo differs)"
check "lines end with LF only" "0" "$(grep -c $'\r' "$work/out.json")"

# Everyday names: names/<foodGroup>.json beside the output file sets `name` only.
mkdir "$work/names"
printf '{\n"10-001":"White bread",\n"10-006":"Plain food"\n}\n' > "$work/names/AA.json"
printf '{\n"13-669#2":"Watercress"\n}\n' > "$work/names/DG.json"
seed ok > /dev/null
check "names from the overlays are applied; originalName and sourceId stay" '"sourceId":"10-001","name":"White bread","originalName":"Bread, white"
"sourceId":"10-006","name":"Plain food","originalName":"Nothing optional"
"sourceId":"13-669","name":"Aubergine, roasted","originalName":"Aubergine, roasted"
"sourceId":"13-669#2","name":"Watercress","originalName":"Watercress, raw"' "$(grep -oE '"sourceId":"(10-001|10-006|13-669|13-669#2)","name":"[^"]*","originalName":"[^"]*"' "$work/out.json")"
cp "$work/out.json" "$work/named.json"
seed ok > /dev/null
check "regenerating keeps the names and is byte-identical" "same" "$(cmp -s "$work/named.json" "$work/out.json" && echo same || echo differs)"
check "--names reads another folder" "same" "$(python3 -I "$script" "$work/book.xlsx" "$work/other.json" --names "$work/names" 2> /dev/null; cmp -s "$work/named.json" "$work/other.json" && echo same || echo differs)"

overlay() { rm -f "$work"/names/*.json; printf '%s\n' "$2" > "$work/names/$1.json"; seed ok | tail -2; }
check "a code that is not in the workbook output fails" "build-food-seed: $work/names/AA.json: 10-999 is not a food in the workbook output
exit=1" "$(overlay AA '{"10-999":"Nothing"}')"
check "a food that was dropped for a missing value fails" "build-food-seed: $work/names/AA.json: 10-002 is not a food in the workbook output
exit=1" "$(overlay AA '{"10-002":"Nothing"}')"
check "an entry in another group's file fails" "build-food-seed: $work/names/DG.json: 10-001 is in group AA, not DG
exit=1" "$(overlay DG '{"10-001":"White bread"}')"
check "an empty name fails" "build-food-seed: $work/names/AA.json: 10-001 has an empty name
exit=1" "$(overlay AA '{"10-001":" "}')"
check "a padded overlay name is written unpadded" '"sourceId":"10-001","name":"White bread"' "$(overlay AA '{"10-001":" White bread "}' > /dev/null; grep -oE '"sourceId":"10-001","name":"[^"]*"' "$work/out.json")"
check "a repeated sourceId key in an overlay fails" "build-food-seed: cannot read $work/names/AA.json: repeated key 10-001
exit=1" "$(overlay AA '{"10-001":"White bread","10-001":"Bread"}')"
rm -rf "$work/names"
check "a --names folder that does not exist fails" "build-food-seed: names folder $work/nonexistent does not exist
exit=1" "$(python3 -I "$script" "$work/book.xlsx" "$work/x.json" --names "$work/nonexistent" 2>&1 | tail -1; echo "exit=${PIPESTATUS[0]}")"

check "a missing sheet fails" "build-food-seed: sheet '1.4 Inorganics' is not in the workbook
exit=1" "$(seed nosheet)"
check "a missing tag fails" "build-food-seed: tag AOACFIB is not in row 2 of '1.3 Proximates'
exit=1" "$(seed notag)"
check "a food listed twice under the same code and name fails" "warning: food 10-008 'Not in inorganics' (row 13) has no row in '1.4 Inorganics'; its saltG is null
warning: food code 13-669 is repeated; 'Watercress, raw' (row 15) gets sourceId 13-669#2
warning: food code 13-669 is repeated; 'Third with the code' (row 16) gets sourceId 13-669#3
build-food-seed: '1.3 Proximates' row 18: food 17-001 'Lager, 4%' is listed twice
exit=1" "$(seed dupkey)"
check "a food listed twice in 1.4 fails" "build-food-seed: '1.4 Inorganics' row 17: food 17-001 'Lager, 4%' is listed twice
exit=1" "$(seed dupinorganics)"
check "a value that is not a number fails" "build-food-seed: '1.3 Proximates' row 6 KCALS: cannot read '12 kcal' as a number
exit=1" "$(seed notnumber)"
check "a negative value fails" "build-food-seed: '1.3 Proximates' row 6 KCALS: '-1' is not a non-negative number
exit=1" "$(seed negative)"
check "a renamed Group header fails" "build-food-seed: '1.3 Proximates' column D is 'Grp', expected 'Group'
exit=1" "$(seed badgroup)"
check "a tag in two columns of row 2 fails" "build-food-seed: tag FAT is in more than one column of '1.3 Proximates'
exit=1" "$(seed duptag)"
check "a blank food name fails" "build-food-seed: '1.3 Proximates' row 4: food 17-001 has no name
exit=1" "$(seed noname)"
check "a renamed 1.4 name header fails" "build-food-seed: '1.4 Inorganics' column B is 'Name', expected 'Food Name'
exit=1" "$(seed badinorganics)"
check "a file that is not a zip fails" "build-food-seed: cannot open WORK/book.xlsx: File is not a zip file
exit=1" "$(seed notzip | sed "s|$work|WORK|")"
check "a wrong number of arguments prints the usage" "exit=1" "$(python3 -I "$script" 2>/dev/null; echo "exit=$?")"

finish
