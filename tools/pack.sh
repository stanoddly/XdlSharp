#!/usr/bin/env bash
# Packs XdlSharp and an XdlSharp.Native.<rid> package for each runtime of the XDL release into <output>.
# usage: tools/pack.sh <output> [version]
# Without a version, or with an empty one, the packages are marked local, as a release passes <SDL version>.<N>.
set -euo pipefail

output=$(realpath -m "$1")
version_argument=()
if [[ -n "${2:-}" ]]; then
  version_argument=("-p:Version=$2")
fi

root=$(dirname "$(realpath "$0")")/..
# The runtimes of every XDL release; the browser one is the XDL target browser-wasm-net11.
runtimes=(linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64 win-arm64 browser-wasm)

dotnet pack "$root/src/XdlSharp" -c Release -o "$output" "${version_argument[@]}"
for runtime in "${runtimes[@]}"; do
  dotnet pack "$root/packaging/XdlSharp.Native" -c Release -o "$output" "-p:XdlRuntimeIdentifier=$runtime" "${version_argument[@]}"
done
