#!/usr/bin/env bash
# Opus 0.1 - runs the alpha-host smoke through Opus.App.OpusAlpha.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
cd "${repo_root}"
export CI=true
export PATH="/c/Program Files/dotnet:${PATH}"
dotnet run \
    --project src/Apps/Opus.App.OpusAlpha/Opus.App.OpusAlpha.csproj \
    -c Release --no-build -- smoke "$@"
