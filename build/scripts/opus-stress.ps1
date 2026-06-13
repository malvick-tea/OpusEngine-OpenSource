# Opus 0.1 - runs the M11 alpha-host stress harness through Opus.App.OpusAlpha.
# Default invocation drives the configured iteration count over fresh D3D12 host
# instances and writes a paired JSON+TXT stress report under the diagnostics root.
# Caller can pass additional CLI options through after the separator (e.g.
# --iterations, --frames, --scene large, --known-issues path).
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath '..')
Set-Location $repoRoot
$env:CI = 'true'
dotnet run --project src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -c Release --no-build -- stress @args
