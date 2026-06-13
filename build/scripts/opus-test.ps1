# Opus 0.1 - scripted full test suite.
# Assumes opus-build.ps1 (or an equivalent Release build) has produced the
# binaries; we re-run dotnet test with --no-build so this script reports test
# outcomes only.
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath '..')
Set-Location $repoRoot
$env:CI = 'true'
dotnet test OpusEngine.sln -c Release --no-build
