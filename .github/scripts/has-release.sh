#!/usr/bin/env bash
# Usage: has-release.sh
# Prints found=true when a v* tag exists before HEAD, else found=false, in the form of a GitHub Actions output
# (has-release.sh >> "$GITHUB_OUTPUT"). The upgrade check needs a last release to install first, so with none yet
# its jobs skip their steps. Needs the tags (fetch-depth: 0).
set -euo pipefail
if git describe --tags --abbrev=0 --match 'v[0-9]*' HEAD > /dev/null 2>&1; then
  echo "found=true"
else
  echo "found=false"
  echo "::notice title=Upgrade::No v* release tag yet, so there is no last release to upgrade from: skipping the upgrade check." >&2
fi
