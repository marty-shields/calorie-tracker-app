#!/usr/bin/env bash
# check-food-names.py against a small seed and names folder built in a temp folder.
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib.sh"
script="$here/../check-food-names.py"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# food <sourceId> <name> <originalName> <group>
food() { printf '{"source":"CoFID","sourceId":"%s","name":"%s","originalName":"%s","foodGroup":"%s"}' "$1" "$2" "$3" "$4"; }

# setup <seed foods, comma-separated> <AA overlay JSON>: a fresh seed and names/AA.json
setup() {
  rm -rf "${work:?}/names" && mkdir "$work/names"
  printf '{"seedVersion":1,"source":"CoFID 2021","foods":[\n%s\n]}\n' "$1" > "$work/seed.json"
  printf '%s\n' "$2" > "$work/names/AA.json"
}

run() { python3 -I "$script" --seed "$work/seed.json" --names "$work/names" "$@" 2>&1; echo "exit=$?"; }

bread=$(food 10-001 "White bread" "Bread, white" AA)
milk=$(food 10-002 "Whole milk" "Milk, whole" AA)
ok_overlay='{"10-001":"White bread","10-002":"Whole milk"}'

setup "$bread,
$milk" "$ok_overlay"
check "every food named passes" "exit=0" "$(run)"

setup "$bread,
$(food 10-002 "Milk, whole" "Milk, whole" AA)" '{"10-001":"White bread"}'
check "a food with no name fails in strict mode" "10-002: no everyday name (add it to names/AA.json)
exit=1" "$(run)"
check "--allow-missing passes it" "exit=0" "$(run --allow-missing)"

setup "$bread,
$(food 10-002 "White bread" "Milk, whole" AA)" '{"10-001":"White bread","10-002":"white  Bread"}'
check "a duplicate name lists both ids, ignoring case and spacing" "10-002: seed name \"White bread\" differs from its overlay \"white  Bread\" (regenerate the seed)
10-001 and 10-002: the same name \"White bread\"
exit=1" "$(run)"

setup "$bread,
$(food 10-002 "Milk, whole" "Milk, whole" AA)" '{"10-001":"White bread"}'
check "an unnamed food is not a duplicate of a named one in --allow-missing" "exit=0" "$(run --allow-missing)"

setup "$bread,
$milk" '{"10-001":"White bread","10-002":"Whole milk","10-009":"Ghost"}'
check "an overlay code that is not in the seed fails" "AA.json: 10-009 is not in the seed
exit=1" "$(run)"

setup "$bread,
$milk" '{"10-001":"White bread","10-002":"Skimmed milk"}'
check "a seed name that differs from the overlay fails" "10-002: seed name \"Whole milk\" differs from its overlay \"Skimmed milk\" (regenerate the seed)
exit=1" "$(run)"

setup "$bread,
$(food 10-002 "Milk" "Milk, whole" AA)" '{"10-001":"White bread"}'
check "a hand-edited seed name with no overlay entry fails" "10-002: seed name \"Milk\" has no overlay entry (edit names/, then regenerate)
exit=1" "$(run --allow-missing)"

setup "$(food 10-001 "Bread, white" "Bread, white" AA)" '{"10-001":"Bread, white"}'
check "a comma fails" "10-001: \"Bread, white\" has a comma
exit=1" "$(run)"

setup "$(food 10-001 "Yogurt" "Yoghurt" AA)" '{"10-001":"Yogurt"}'
check "an American spelling fails" "10-001: \"Yogurt\" uses American spelling \"yogurt\" (British: yoghurt)
exit=1" "$(run)"

long=$(printf 'a%.0s' {1..81})
setup "$(food 10-001 "$long" "Bread" AA)" "{\"10-001\":\"$long\"}"
check "a name over 80 characters fails" "10-001: \"$long\" is 81 characters (at most 80)
exit=1" "$(run)"

setup "$(food 10-001 "$(printf 'a%.0s' {1..80})" "Bread" AA)" "{\"10-001\":\"$(printf 'a%.0s' {1..80})\"}"
check "80 characters is allowed" "exit=0" "$(run)"

setup "$bread" '{"10-001":"White bread"}'
printf '{"10-001":"White bread"}\n' > "$work/names/BA.json"
check "an entry in another group's file fails" "BA.json: 10-001 is in group AA, not BA
exit=1" "$(run)"

setup "$bread" '{"10-001":""}'
check "an empty name fails" "AA.json: 10-001 has an empty name
10-001: no everyday name (add it to names/AA.json)
exit=1" "$(run)"

rm -rf "$work/names"
setup "$(food 10-001 "Bread" "Bread" AA)" '{}'
rm -rf "$work/names"
check "no names folder passes with --allow-missing" "exit=0" "$(run --allow-missing)"

check "an unknown option prints the usage" "exit=1" "$(python3 -I "$script" --bogus 2>/dev/null; echo "exit=$?")"

finish
