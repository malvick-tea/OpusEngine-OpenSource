# Opus 0.1 - runs the alpha-host smoke through Opus.App.OpusAlpha.
# The smoke steps 60 frames against the procedural fallback asset and writes a
# paired JSON+TXT smoke report under the diagnostics root. Caller can pass
# additional CLI options through after the separator.
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath '..')
Set-Location $repoRoot
$env:CI = 'true'
dotnet run --project src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -c Release --no-build -- smoke @args
