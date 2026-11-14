#!/usr/bin/env bash
# check-native-dupes.sh on throwaway folders laid out like the Android build's unpacked libraries.
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib.sh"
script="$here/../check-native-dupes.sh"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# lib <case> <package number> <abi> <name> <content>
lib() {
  local dir="$work/$1/Release/lp/$2/jl/jni/$3"
  mkdir -p "$dir"
  printf '%s' "$5" > "$dir/$4"
}

# run <case>: the exit code, then the output with the work folder removed.
run() { local out; out=$(bash "$script" "$work/$1" 2>&1); echo "exit=$?"; echo "${out//$work/}"; }

lib same 1 arm64-v8a libx.so aaa
lib same 2 arm64-v8a libx.so aaa
lib same 2 arm64-v8a liby.so bbb
lib differ 1 arm64-v8a libx.so aaa
lib differ 2 arm64-v8a libx.so aab
lib abis 1 arm64-v8a libx.so aaa
lib abis 2 x86_64 libx.so zzz
mkdir -p "$work/empty"

check "identical copies pass" $'exit=0\n3 libraries, 1 duplicated, all identical' "$(run same)"
check "copies that differ fail and are named" $'exit=1\n::error title=Native duplicates::Copies of libx.so for arm64-v8a differ in /differ/Release; XA4301 can no longer be hidden.\n  38760eabb666e8e61ee628a17c4090cc50728e095ff24218119d51bd22475363  /differ/Release/lp/2/jl/jni/arm64-v8a/libx.so\n  9834876dcfb05cb167a5c24953eba58c4ac89b1adf57f28f2f9d09af107ee8f0  /differ/Release/lp/1/jl/jni/arm64-v8a/libx.so' "$(run differ)"
check "different ABIs are not compared" $'exit=0\n2 libraries, 0 duplicated, all identical' "$(run abis)"
check "no libraries fails" $'exit=1\n::error title=Native duplicates::No unpacked native libraries under /empty; build the App first.' "$(run empty)"
check "missing folder fails" $'exit=1\n::error title=Native duplicates::/nope is not a folder; build the App first.' "$(run nope)"

finish
