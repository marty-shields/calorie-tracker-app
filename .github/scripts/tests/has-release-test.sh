#!/usr/bin/env bash
# has-release.sh in a throwaway repo: whether a v* tag exists before HEAD.
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib.sh"
script="$here/../has-release.sh"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
repo="$work/repo"
git init -q "$repo"
git -C "$repo" config user.email test@example.com
git -C "$repo" config user.name Test
git -C "$repo" commit -q --allow-empty -m first

run() { (cd "$repo" && bash "$script" 2> /dev/null); }

check "no tag" "found=false" "$(run)"

git -C "$repo" tag not-a-release
check "a tag that isn't v*" "found=false" "$(run)"

git -C "$repo" tag v1.0.0
git -C "$repo" commit -q --allow-empty -m second
check "a v* tag before HEAD" "found=true" "$(run)"

finish
