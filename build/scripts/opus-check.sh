#!/usr/bin/env bash
# Opus 0.1 - captures the current machine profile and compares against a
# reference (see --reference) when supplied.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
cd "${repo_root}"
export CI=true
export PATH="/c/Program Files/dotnet:${PATH}"
dotnet run \
    --project src/Apps/Opus.App.OpusAlpha/Opus.App.OpusAlpha.csproj \
    -c Release --no-build -- check-machine "$@"
