#!/usr/bin/env bash
# Packs XdlSharp and an XdlSharp.Native.<rid> package for each runtime of the XDL release into <output>.
# usage: tools/pack.sh <output> [version]
# Without a version, or with an empty one, the version is marked local with the time, such as 3.4.20-local.20261109103000, so a
# consumer never restores an older local pack from its package cache. A release passes <SDL version>.<N>.
set -euo pipefail

mkdir -p "${1:?usage: tools/pack.sh <output> [version]}"
output=$(CDPATH= cd "$1" && pwd)
root=$(CDPATH= cd "$(dirname "$0")/.." && pwd)
if [[ -n "${2:-}" ]]; then
  version_argument="-p:Version=$2"
else
  version_argument="-p:VersionSuffix=local.$(date -u +%Y%m%d%H%M%S)"
fi

# The runtimes of every XDL release; the browser one is the XDL target browser-wasm-net11.
runtimes=(linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64 win-arm64 browser-wasm)

dotnet pack "$root/src/XdlSharp" -c Release -o "$output" "$version_argument"
for runtime in "${runtimes[@]}"; do
  dotnet pack "$root/packaging/XdlSharp.Native" -c Release -o "$output" "-p:XdlRuntimeIdentifier=$runtime" "$version_argument"
done
