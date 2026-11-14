#!/usr/bin/env bash
# build-food-seed.py against a small CoFID-shaped workbook that this test builds in a temp folder (no binary is committed).
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib.sh"
script="$here/../build-food-seed.py"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# The workbook generator: mkbook.py <out.xlsx> <variant>, variant ok | nosheet | notag | dupkey | dupinorganics | notnumber | negative | badgroup | duptag | noname | badinorganics | notzip.
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
check "the seed file" '{"seedVersion":1,"source":"CoFID 2021","foods":[
{"source":"CoFID","sourceId":"10-001","name":"Bread, white","originalName":"Bread, white","foodGroup":"AA","unit":"Gram","energyKcal":100,"proteinG":0.07,"carbsG":4.6,"fatG":0.13,"fibreG":2.5,"sugarsG":0,"saturatesG":0.5,"saltG":1},
{"source":"CoFID","sourceId":"10-006","name":"Nothing optional","originalName":"Nothing optional","foodGroup":"AA","unit":"Gram","energyKcal":5,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":null},
{"source":"CoFID","sourceId":"10-007","name":"Trace sodium","originalName":"Trace sodium","foodGroup":"AA","unit":"Gram","energyKcal":5,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":0.05,"saturatesG":null,"saltG":0},
{"source":"CoFID","sourceId":"10-008","name":"Not in inorganics","originalName":"Not in inorganics","foodGroup":"AQ","unit":"Gram","energyKcal":5,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":null},
{"source":"CoFID","sourceId":"13-669","name":"Aubergine, roasted","originalName":"Aubergine, roasted","foodGroup":"DG","unit":"Gram","energyKcal":62,"proteinG":2.1,"carbsG":5.2,"fatG":3.8,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.01},
{"source":"CoFID","sourceId":"13-669#2","name":"Watercress, raw","originalName":"Watercress, raw","foodGroup":"DG","unit":"Gram","energyKcal":10,"proteinG":1.9,"carbsG":0,"fatG":0.3,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.09},
{"source":"CoFID","sourceId":"13-669#3","name":"Third with the code","originalName":"Third with the code","foodGroup":"DG","unit":"Gram","energyKcal":7,"proteinG":1,"carbsG":1,"fatG":1,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":null},
{"source":"CoFID","sourceId":"17-001","name":"Lager, 4%","originalName":"Lager, 4%","foodGroup":"QA","unit":"Millilitre","energyKcal":29,"proteinG":0.3,"carbsG":1.5,"fatG":0,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.01},
{"source":"CoFID","sourceId":"17-002","name":"Whisky","originalName":"Whisky","foodGroup":"QK","unit":"Millilitre","energyKcal":222,"proteinG":0,"carbsG":0,"fatG":0,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0},
{"source":"CoFID","sourceId":"18-001","name":"Alcohol-free","originalName":"Alcohol-free","foodGroup":"QA","unit":"Millilitre","energyKcal":7,"proteinG":0,"carbsG":1,"fatG":0,"fibreG":null,"sugarsG":null,"saturatesG":null,"saltG":0.25}
]}' "$(cat "$work/out.json")"

check "a food with an N or blank energy, protein, carbs or fat is dropped" "" "$(grep -E '10-00[2-5]' "$work/out.json")"
check "the group must start with Q to be a Millilitre food (AQ is not)" "3" "$(grep -c '"unit":"Millilitre"' "$work/out.json")"

cp "$work/out.json" "$work/first.json"
seed ok > /dev/null
check "a second run gives identical bytes" "same" "$(cmp -s "$work/first.json" "$work/out.json" && echo same || echo differs)"
check "lines end with LF only" "0" "$(grep -c $'\r' "$work/out.json")"

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
