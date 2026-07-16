#!/usr/bin/env bash
# Package-consumption smoke test: installs the freshly packed .nupkg files into a
# brand-new console project from a local feed, builds it, and runs it.
# Usage: build/consumption-test.sh <artifacts-dir>
set -euo pipefail

ARTIFACTS="$(cd "${1:?usage: consumption-test.sh <artifacts-dir>}" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Determine the packed version from the core package file name.
core=$(ls "$ARTIFACTS"/Koras.Results.[0-9]*.nupkg | head -1)
VERSION=$(basename "$core" .nupkg | sed 's/^Koras\.Results\.//')
echo "Testing consumption of version $VERSION"

mkdir -p "$WORK/consumer"
cd "$WORK/consumer"

cat > NuGet.Config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$ARTIFACTS" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

dotnet new console -n Consumer --framework net10.0 -o . >/dev/null
dotnet add package Koras.Results --version "$VERSION" >/dev/null

cat > Program.cs <<'EOF'
using Koras.Results;

Result<int> parsed = Result.Try(() => int.Parse("42"));
Result<int> doubled = parsed
    .Ensure(v => v > 0, Error.Validation("Number.NotPositive", "Value must be positive."))
    .Map(v => v * 2);

string message = doubled.Match(
    v => $"CONSUMPTION-OK {v}",
    e => $"CONSUMPTION-FAIL {e.Code}");
Console.WriteLine(message);

Result failure = Error.NotFound("Thing.NotFound", "Missing.");
if (failure.IsFailure && failure.Error.Type == ErrorType.NotFound)
{
    Console.WriteLine("TAXONOMY-OK");
}
EOF

dotnet build -c Release >/dev/null
output=$(dotnet run -c Release --no-build)
echo "$output"

echo "$output" | grep -q "CONSUMPTION-OK 84" || { echo "::error::consumption smoke test failed"; exit 1; }
echo "$output" | grep -q "TAXONOMY-OK" || { echo "::error::taxonomy smoke test failed"; exit 1; }
echo "Package consumption test passed."
