#!/usr/bin/env bash
# Usage: check-native-dupes.sh <obj-folder>
# The App hides XA4301 (the same native library in several packages). This fails unless every such duplicate is
# byte-identical: it looks at the libraries the Android build unpacked from each package's AAR (<build>/lp/<n>/jl/jni/<abi>/<name>),
# groups them by build folder, ABI and file name, and names any group whose copies hash differently.
set -euo pipefail
root="$1"
[[ -d "$root" ]] || { echo "::error title=Native duplicates::$root is not a folder; build the App first."; exit 1; }

# One line per library, tab-separated: <build>|<abi>|<name>, <sha256>, <path>
lines=$(find "$root" -path '*/lp/*/jl/jni/*/*.so' -type f -print0 | while IFS= read -r -d '' f; do
  build="${f%%/lp/*}"
  rest="${f#*/jl/jni/}"
  printf '%s|%s|%s\t%s\t%s\n' "$build" "${rest%%/*}" "${rest#*/}" "$(sha256sum "$f" | cut -d' ' -f1)" "$f"
done | sort)
[[ -n "$lines" ]] || { echo "::error title=Native duplicates::No unpacked native libraries under $root; build the App first."; exit 1; }

bad=$(awk -F'\t' '{ key = $1; n[key]++; if (!(key in first)) first[key] = $2; else if ($2 != first[key]) differ[key] = 1 }
  END { for (k in differ) print k }' <<<"$lines" | sort)
if [[ -n "$bad" ]]; then
  while IFS= read -r key; do
    name="${key##*|}"
    abi="${key#*|}"
    abi="${abi%%|*}"
    echo "::error title=Native duplicates::Copies of $name for $abi differ in ${key%%|*}; XA4301 can no longer be hidden."
    awk -F'\t' -v k="$key" '$1 == k { print "  " $2 "  " $3 }' <<<"$lines"
  done <<<"$bad"
  exit 1
fi
total=$(wc -l <<<"$lines")
groups=$(awk -F'\t' '{ n[$1]++ } END { c = 0; for (k in n) if (n[k] > 1) c++; print c }' <<<"$lines")
echo "$total libraries, $groups duplicated, all identical"
