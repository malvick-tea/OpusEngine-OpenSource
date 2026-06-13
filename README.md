# Opus Engine Open Source

Opus is a compact, modular C#/.NET 8 engine workspace with a live Direct3D 12
host, a content packaging toolchain, networking and diagnostics layers, and the
first public drop of the Opus UI Edition editor.

This repository is not a finished product release. It is a working engine
snapshot: useful to read, build, test, modify, and learn from, but still early
enough that APIs can move. The code is intentionally kept source-first and
engine-focused; consumer game assets and the original private planning documents
are not part of this public tree.

## What Is Included

| Area | What it gives you |
| --- | --- |
| Runtime host | `OpusHost`, fixed-tick stepping, lifecycle callbacks, pause/resume/shutdown policy |
| Platform layer | PAL contracts plus SDL3 and Windows/D3D12 window/session adapters |
| Rendering | Backend-neutral RHI contracts, D3D12 device/swap-chain/frame-graph code, forward scene rendering, tonemapping, screenshots |
| UI | Backend-neutral draw contracts plus a D3D12 immediate draw surface, glyph atlas, text layout, ink and world-space text helpers |
| Content | glTF/GLB helpers, mesh and animation data, image/mip/BC compression helpers |
| Packaging | Opus package manifests, validation, `.opkg` archive pack/verify/unpack, signing support, localized diagnostics |
| Networking | Transport contracts, loopback transport, UDP client/server transport, sessions, telemetry, reconnect and soak harnesses |
| Diagnostics | Overlay models, failure reports, evidence paths, rolling logs, alpha frame budget checks |
| Consumer integration | Reflection-loaded external consumer assemblies for lifecycle, scene, asset, and telemetry data |
| Opus Alpha Host | A runnable Windows/D3D12 harness for smoke, stress, package, machine, and diagnostics workflows |
| Opus UI Editor | Headless scene/project/animation authoring core, live D3D12 editor window, CLI authoring commands, pseudo-code mirrors |

## Repository Shape

```text
src/
  Apps/          Runnable alpha host and its tests
  Content/       Asset readers, package validation, archives, signing
  Editor/        UI Edition editor core, UI model, D3D12 surface, app CLI
  Engine/        Runtime, PAL, RHI, renderer, UI, diagnostics, net, physics
  Foundation/    Shared diagnostics, geometry, logging, time, identity
  Localisation/  Translation catalog contracts and CSV catalog
  Net/           Transport contracts, loopback, UDP implementation
  Persistence/   Save/settings codecs and file-store contracts
  Tools/         Package validator CLI

docs/
  OPERATING.md              Build, test, run, troubleshoot
  EDITOR.md                 Opus UI Editor guide
  RENDERING.md              RHI, D3D12 renderer, UI draw surface
  CONTENT_PACKAGING.md      Manifest, validation, archive, signing workflow
  CONSUMER_INTEGRATION.md   External consumer assembly boundary
```

There are 67 projects in the solution after the editor import. The public tree
currently carries more than a thousand C# files and a full test suite covering
the engine, tools, editor core, editor UI, D3D12 seams, and app-level CLIs.

## Requirements

- .NET SDK 8.
- Windows for live D3D12 windows and D3D12 smoke tests.
- A Direct3D 12 capable adapter for the alpha host and editor window.

Most headless libraries and tests build cross-platform, but the production
graphics path is D3D12.

## Quick Start

From the repository root:

```powershell
$env:CI = 'true'
dotnet restore .\OpusEngine.sln
dotnet build .\OpusEngine.sln -c Release -t:Rebuild
dotnet test .\OpusEngine.sln -c Release --no-build -m:1
```

`CI=true` turns warnings into errors through `Directory.Build.props`. The
serialized test run (`-m:1`) is the least noisy way to run the whole suite when
network or D3D12 integration tests are present.

The latest local verification of this public workspace after the editor import:

```text
Release rebuild: 0 warnings, 0 errors
Full tests:      2245 passed, 0 failed, 0 skipped
```

Generated build output lives under `build/output/`. Do not delete the whole
`build/` directory when cleaning, because the source tree also contains
`build/scripts/` and `build/known-good/`.

```powershell
Remove-Item -Recurse -Force .\build\output
```

## Run The Alpha Host

Show help:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --help
```

Open the live D3D12 host:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj
```

Run a smoke pass:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --frames 60
```

Load an external consumer integration assembly:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --consumer <path-to-assembly>
```

## Run The Editor

Show editor help:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- --help
```

Open the live authoring window:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- window
```

Open a scene with a project and persistent window settings:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- window .\sample.scene.json --project .\sample.opusproj.json --settings .\.local\editor.settings.json --lang en
```

Useful editor commands:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- new .\garage.scene.json --name Garage
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- place .\garage.scene.json .\models\tank.glb --name Tank --at 0,0,0
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- show .\garage.scene.json
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- project-doctor .\sample.opusproj.json --content-root .\content
```

The editor is split into four seams:

- `Opus.Editor.Core`: pure scene/project/animation documents, commands,
  undo/redo, serializers, validators, pseudo-code writers.
- `Opus.Editor.Ui`: pure viewport, outliner, inspector, toolbar, selection,
  gizmo, layout, and input mapping logic.
- `Opus.Editor.Direct3D12`: the D3D12 draw surface for the live editor window.
- `Opus.App.Editor`: CLI, filesystem IO, settings, autosave, project workspace,
  and live window loop.

See [`docs/EDITOR.md`](docs/EDITOR.md) for the editor workflow and architecture.

## Package Tool

Show help:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- --help
```

Common commands:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- validate <package-root>
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- generate <content-root> --id <id> --name <name> --version <semver>
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- pack <content-root> --output <package.opkg>
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- verify <package.opkg>
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- unpack <package.opkg> <target-dir>
```

## Design Principles

- Keep contracts separate from backends.
- Keep D3D12-specific code in D3D12 assemblies.
- Keep platform-specific code in PAL assemblies.
- Keep authoring models pure and testable; app projects own filesystem and
  window IO.
- Keep package validation headless and deterministic.
- Keep consumer integration behind explicit contracts; do not hardcode one game.
- Prefer small assemblies and focused tests at the boundary where behavior lives.

## Documentation

- [`docs/OPERATING.md`](docs/OPERATING.md) - build, test, run, local workflows,
  troubleshooting.
- [`docs/EDITOR.md`](docs/EDITOR.md) - UI Edition editor architecture,
  commands, shortcuts, file formats, testing.
- [`docs/RENDERING.md`](docs/RENDERING.md) - RHI, frame graph, D3D12 renderer,
  UI draw surface, screenshots.
- [`docs/CONTENT_PACKAGING.md`](docs/CONTENT_PACKAGING.md) - manifests,
  validation, archive/signing flow, package CLI.
- [`docs/CONSUMER_INTEGRATION.md`](docs/CONSUMER_INTEGRATION.md) - external
  consumer module boundary for the alpha host.

## Contributing

Read [`CONTRIBUTORS.md`](CONTRIBUTORS.md) for attribution notes and keep changes
small enough to review. A good change usually builds the touched project, passes
the closest test project, and adds tests for new public behavior.
