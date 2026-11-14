#!/usr/bin/env bash
# coverage-summary.py against small Cobertura reports in a temp folder. The gate is the fixed 95% minimum.
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib.sh"
script="$here/../coverage-summary.py"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# report <file> <line-rate> [branch-rate]
report() {
  printf '<coverage><packages><package name="CalorieTracker.Core" line-rate="%s" branch-rate="%s"/><package name="Other" line-rate="0.1" branch-rate="0.1"/></packages></coverage>\n' "$2" "${3-0.5}" > "$work/$1"
}

# gate <report rate>: the output and exit code of the gated run
gate() {
  report core.xml "$1"
  python3 -I "$script" --gate CalorieTracker.Core="$work/core.xml" 2>/dev/null; echo "exit=$?"
}

check "exactly the minimum passes" '| Assembly | Lines | Branches | Minimum lines |
| --- | ---: | ---: | ---: |
| CalorieTracker.Core | 95.0% | 50.0% | 95.0% |
exit=0' "$(gate 0.95)"

check "a rise passes" '| Assembly | Lines | Branches | Minimum lines |
| --- | ---: | ---: | ---: |
| CalorieTracker.Core | 98.7% | 50.0% | 95.0% |
exit=0' "$(gate 0.987)"

check "a value that rounds up to the minimum passes" '| Assembly | Lines | Branches | Minimum lines |
| --- | ---: | ---: | ---: |
| CalorieTracker.Core | 95.0% | 50.0% | 95.0% |
exit=0' "$(gate 0.9496)"

check "any drop below the minimum fails" '| Assembly | Lines | Branches | Minimum lines |
| --- | ---: | ---: | ---: |
| CalorieTracker.Core | 94.8% | 50.0% | 95.0% |

**CalorieTracker.Core line coverage 94.8% is below the minimum 95.0%.**
exit=1' "$(gate 0.948)"

for bad in NaN inf -inf "" abc; do
  report core.xml "$bad"
  check "a line-rate of '$bad' fails" "exit=1" "$(python3 -I "$script" --gate CalorieTracker.Core="$work/core.xml" 2>/dev/null; echo "exit=$?")"
done
for bad in NaN inf "" abc; do
  report core.xml 0.99 "$bad"
  check "a branch-rate of '$bad' fails" "exit=1" "$(python3 -I "$script" --gate CalorieTracker.Core="$work/core.xml" 2>/dev/null; echo "exit=$?")"
done
report core.xml NaN
check "a NaN line-rate says why" "CalorieTracker.Core has a line-rate of NaN in $work/core.xml, not a number" "$(python3 -I "$script" --gate CalorieTracker.Core="$work/core.xml" 2>&1)"

report core.xml 0.5
check "no option prints the table without a gate" '| Assembly | Lines | Branches |
| --- | ---: | ---: |
| CalorieTracker.Core | 50.0% | 50.0% |
exit=0' "$(python3 -I "$script" CalorieTracker.Core="$work/core.xml"; echo "exit=$?")"

finish
