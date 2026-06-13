#!/usr/bin/env bash
# Opus 0.1 - scripted clean Release build.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
cd "${repo_root}"
export CI=true
export PATH="/c/Program Files/dotnet:${PATH}"
dotnet build OpusEngine.sln -c Release --no-restore
