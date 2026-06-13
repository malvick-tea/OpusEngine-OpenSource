# Opus 0.1 - scripted clean Release build.
# Mirrors the canonical M9 verification command. CI=true turns
# TreatWarningsAsErrors on, so the script exits non-zero on the first warning.
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath '..')
Set-Location $repoRoot
$env:CI = 'true'
dotnet build OpusEngine.sln -c Release --no-restore
