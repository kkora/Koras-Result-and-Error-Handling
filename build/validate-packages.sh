#!/usr/bin/env bash
# Validates the contents and metadata of every produced .nupkg.
# Usage: build/validate-packages.sh <artifacts-dir>
set -euo pipefail

ARTIFACTS="${1:?usage: validate-packages.sh <artifacts-dir>}"
FAIL=0

shopt -s nullglob
PACKAGES=("$ARTIFACTS"/*.nupkg)
if [ ${#PACKAGES[@]} -eq 0 ]; then
  echo "::error::No .nupkg files found in $ARTIFACTS"
  exit 1
fi

EXPECTED_TFMS=(net8.0 net9.0 net10.0)

for pkg in "${PACKAGES[@]}"; do
  name=$(basename "$pkg")
  echo "── Validating $name"
  listing=$(unzip -l "$pkg")

  check() {
    local pattern="$1" label="$2"
    if echo "$listing" | grep -qE "$pattern"; then
      echo "   ok: $label"
    else
      echo "::error::$name is missing $label"
      FAIL=1
    fi
  }

  check 'icon\.png' "package icon"
  check 'README\.md' "package README"
  check 'LICENSE|\.nuspec' "nuspec"

  for tfm in "${EXPECTED_TFMS[@]}"; do
    check "lib/$tfm/.*\.dll" "assembly for $tfm"
    check "lib/$tfm/.*\.xml" "XML documentation for $tfm"
  done

  # Metadata assertions from the embedded nuspec
  nuspec=$(unzip -p "$pkg" '*.nuspec')
  for field in '<license type="expression">MIT</license>' '<projectUrl>' '<repository ' '<readme>README.md</readme>' '<icon>icon.png</icon>' '<tags>'; do
    if echo "$nuspec" | grep -qF "${field}"; then
      echo "   ok: nuspec contains ${field}"
    else
      echo "::error::$name nuspec missing ${field}"
      FAIL=1
    fi
  done

  # Symbols package must exist alongside
  snupkg="${pkg%.nupkg}.snupkg"
  if [ -f "$snupkg" ]; then
    echo "   ok: symbols package"
  else
    echo "::error::missing symbols package for $name"
    FAIL=1
  fi
done

# The core package must have zero NuGet dependencies (ADR-0001).
core_pkg=$(ls "$ARTIFACTS"/Koras.Results.[0-9]* 2>/dev/null | head -1 || true)
if [ -n "$core_pkg" ]; then
  deps=$(unzip -p "$core_pkg" '*.nuspec' | grep -c '<dependency ' || true)
  if [ "$deps" -eq 0 ]; then
    echo "── ok: Koras.Results core has zero package dependencies"
  else
    echo "::error::Koras.Results core must have zero dependencies, found $deps"
    FAIL=1
  fi
fi

exit $FAIL
