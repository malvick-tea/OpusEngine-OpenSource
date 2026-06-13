# Opus 0.1 - captures the current machine profile and (when --reference is
# supplied) compares it against a known-good baseline. The script is a thin
# wrapper around `Opus.App.OpusAlpha check-machine` so testers reach the
# same exit-code shape as CI does.
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath '..')
Set-Location $repoRoot
$env:CI = 'true'
dotnet run --project src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -c Release --no-build -- check-machine @args
