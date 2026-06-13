# Operating Guide

This guide is the day-to-day map for building, testing, and running the public
Opus workspace.

## Local Requirements

Required:

- .NET SDK 8.
- Windows for live D3D12 windows and D3D12 smoke tests.
- A Direct3D 12 capable adapter for the alpha host and editor window.

Useful:

- Visual Studio or Rider for solution navigation.
- PIX, RenderDoc, or another graphics debugger for renderer work.
- A local `content/` folder with glTF/GLB assets and material sets.
- A `.local/` folder for diagnostics, editor settings, screenshots, and scratch
  files that should not be committed.

## First Build

From the repository root:

```powershell
$env:CI = 'true'
dotnet restore .\OpusEngine.sln
dotnet build .\OpusEngine.sln -c Release -t:Rebuild
```

`CI=true` enables warnings-as-errors. `-t:Rebuild` is useful after copying or
syncing files because it avoids stale incremental reference assemblies.

Build output goes under:

```text
build/output/
```

Clean generated output only:

```powershell
Remove-Item -Recurse -Force .\build\output
```

Do not delete the whole `build/` directory when cleaning. The source tree also
contains `build/scripts/` and `build/known-good/`.

## Full Verification

Run the full suite from already-built Release binaries:

```powershell
$env:CI = 'true'
dotnet test .\OpusEngine.sln -c Release --no-build -m:1
```

The serialized run (`-m:1`) is slower but avoids noisy parallelism around
integration tests that touch UDP sockets or live graphics resources.

Latest local verification of this public workspace after the editor import:

```text
Release rebuild: 0 warnings, 0 errors
Full tests:      2245 passed, 0 failed, 0 skipped
```

## Build By Area

Core foundation:

```powershell
dotnet build .\src\Foundation\Opus.Foundation\Opus.Foundation.csproj -c Release
```

Runtime host:

```powershell
dotnet build .\src\Engine\Opus.Engine.Runtime\Opus.Engine.Runtime.csproj -c Release
```

D3D12 RHI:

```powershell
dotnet build .\src\Engine\Opus.Engine.Rhi.D3D12\Opus.Engine.Rhi.D3D12.csproj -c Release
```

D3D12 renderer:

```powershell
dotnet build .\src\Engine\Opus.Engine.Renderer.Direct3D12\Opus.Engine.Renderer.Direct3D12.csproj -c Release
```

Alpha host:

```powershell
dotnet build .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -c Release
```

Editor:

```powershell
dotnet build .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -c Release
```

Package tool:

```powershell
dotnet build .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -c Release
```

## Test By Area

Foundation and shared contracts:

```powershell
dotnet test .\src\Foundation\Opus.Foundation.Tests\Opus.Foundation.Tests.csproj -c Release
dotnet test .\src\Engine\Opus.Engine.Runtime.Tests\Opus.Engine.Runtime.Tests.csproj -c Release
```

Content and packaging:

```powershell
dotnet test .\src\Content\Opus.Content.Tests\Opus.Content.Tests.csproj -c Release
dotnet test .\src\Content\Opus.Content.Packaging.Tests\Opus.Content.Packaging.Tests.csproj -c Release
dotnet test .\src\Tools\Opus.Tool.PackageValidator.Tests\Opus.Tool.PackageValidator.Tests.csproj -c Release
```

Rendering and UI:

```powershell
dotnet test .\src\Engine\Opus.Engine.Renderer.Tests\Opus.Engine.Renderer.Tests.csproj -c Release
dotnet test .\src\Engine\Opus.Engine.Ui.Direct3D12.Tests\Opus.Engine.Ui.Direct3D12.Tests.csproj -c Release
dotnet test .\src\Engine\Opus.Engine.Direct3D12.Tests\Opus.Engine.Direct3D12.Tests.csproj -c Release
dotnet test .\src\Engine\Opus.Engine.Host.Windows.Direct3D12.Tests\Opus.Engine.Host.Windows.Direct3D12.Tests.csproj -c Release
```

Networking:

```powershell
dotnet test .\src\Net\Opus.Net.Tests\Opus.Net.Tests.csproj -c Release
dotnet test .\src\Net\Opus.Net.Udp.Tests\Opus.Net.Udp.Tests.csproj -c Release
dotnet test .\src\Engine\Opus.Engine.Net.Tests\Opus.Engine.Net.Tests.csproj -c Release
```

Editor:

```powershell
dotnet test .\src\Editor\Opus.Editor.Core.Tests\Opus.Editor.Core.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.Editor.Content.Tests\Opus.Editor.Content.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.Editor.Ui.Tests\Opus.Editor.Ui.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.Editor.Direct3D12.Tests\Opus.Editor.Direct3D12.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.App.Editor.Tests\Opus.App.Editor.Tests.csproj -c Release
```

## Alpha Host

Show help:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --help
```

Open the live D3D12 window:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj
```

Open a glTF/GLB asset:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- .\content\models\tank.glb
```

Run a smoke pass:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --frames 60
```

Capture a smoke screenshot:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --frames 60 --screenshot-frame 30
```

Use a diagnostics directory:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --diagnostics-dir .\.local\diagnostics
```

Load a consumer assembly:

```powershell
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --consumer <path-to-assembly>
```

Alpha host modes:

- default live D3D12 window;
- asset-backed live window;
- `smoke`;
- `validate-package`;
- `check-machine`;
- `soak`;
- `stress`;
- `known-issues-merge`;
- `known-issues-diff`;
- external consumer integration through `--consumer`.

## Editor

Show help:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- --help
```

Open the live authoring window:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- window
```

Open with a project and settings:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- window .\scene.json --project .\project.opusproj.json --settings .\.local\editor.settings.json --lang en
```

Create and inspect a scene:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- new .\scene.json --name TestScene
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- place .\scene.json .\content\models\prop.glb --name Prop --at 0,0,0
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- show .\scene.json
```

The editor window supports orbit, pan, zoom, outliner selection, inspector
editing, primitives, model placement, lights, translate/scale/rotate gizmos,
marquee selection, grouping, ungrouping, copy/paste, undo/redo, save/save-as,
screenshots, and a live pseudo-code panel.

See [`EDITOR.md`](EDITOR.md) for the full editor guide.

## Package Tool

Show help:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- --help
```

Validate a package directory:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- validate <package-root>
```

Generate a manifest:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- generate <content-root> --id <id> --name <display-name> --version <semver>
```

Pack a package:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- pack <content-root> --output <package.opkg>
```

Verify an archive:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- verify <package.opkg>
```

Unpack an archive:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- unpack <package.opkg> <target-dir>
```

Use `--format json` on validation when another tool needs structured output.
Use `--locale en|ru` for localized command diagnostics.

## Diagnostics And Output

Common output locations:

```text
build/output/               compiled binaries and intermediate files
.local/diagnostics/         suggested local diagnostics root
.local/editor.settings.json suggested local editor settings file
```

The alpha host can write:

- rolling logs;
- smoke reports;
- stress reports;
- failure reports;
- screenshots;
- machine profile JSON.

Keep generated diagnostics out of source changes unless they are intentionally
small test fixtures.

## Runtime Host Workflow

Use `OpusHost` when adding application lifecycle behavior.

Key callbacks:

```text
IOpusApplication.OnStarted
IOpusApplication.FixedTick
IOpusApplication.Render
IOpusApplication.OnPaused
IOpusApplication.OnResumed
IOpusApplication.OnStopping
```

Rules:

- fixed-tick behavior belongs in `FixedTick`;
- presentation belongs in `Render`;
- PAL window close and lifecycle shutdown request host shutdown;
- `MaxFixedTicksPerFrame` prevents an overloaded frame from draining unlimited
  backlog;
- `DroppedFixedTime` is observable for tests and diagnostics.

## PAL Workflow

Use PAL projects for OS, window, VFS, lifecycle, hardware, process, power,
threading, and platform service work.

Rules:

- shared contracts go in `Opus.Engine.Pal`;
- SDL3 window/input code goes in `Opus.Engine.Pal.Sdl3` or
  `Opus.Engine.Input.Sdl3`;
- Windows filesystem/platform code goes in `Opus.Engine.Pal.Windows`;
- D3D12 window/session glue goes in `Opus.Engine.Pal.Windows.Direct3D12`;
- higher runtime and renderer code should not know about raw OS APIs unless the
  type is explicitly a backend adapter.

## RHI And Renderer Workflow

Use `Opus.Engine.Rhi` for backend-neutral GPU contracts. Use
`Opus.Engine.Rhi.D3D12` for D3D12 implementation.

When adding a D3D12 operation:

1. Decide whether a backend-neutral contract is needed.
2. Keep raw D3D12 handles inside D3D12-specific types.
3. Add or extend root signatures, pipelines, descriptors, command-list helpers,
   or readback code in the matching backend area.
4. Test resource states, descriptor layouts, readback, or planning behavior.
5. Run a D3D12 smoke only when backend behavior changed.

Scene renderer changes usually touch:

- glTF GPU loading;
- material atlas planning;
- instance batching;
- culling;
- LOD selection;
- forward pass;
- tonemap pass;
- screenshot/readback behavior.

## Editor Workflow

Editor changes should start in the pure layer whenever possible.

Rules:

- document shape, commands, serializers, validators, and pseudo-code belong in
  `Opus.Editor.Core`;
- model/material reports belong in `Opus.Editor.Content`;
- viewport behavior, outliner, inspector, toolbar, input, and frame composition
  belong in `Opus.Editor.Ui`;
- live D3D12 drawing belongs in `Opus.Editor.Direct3D12`;
- CLI and filesystem behavior belong in `Opus.App.Editor`;
- a visible authoring mutation should be reversible through the command stack;
- pure behavior should have unit tests before a live window smoke.

## Content And Package Workflow

For low-level content readers:

1. Add parsing code in `Opus.Content`.
2. Keep renderer allocation out of parsers.
3. Test with small byte arrays or generated assets.
4. Return structured errors where possible.

For package behavior:

1. Add manifest or archive types in `Opus.Content.Packaging`.
2. Add validation in the library layer.
3. Add diagnostics with stable codes.
4. Add CLI behavior only after library behavior is tested.
5. Test text and JSON reporters when output changes.

## Networking Workflow

Use `Opus.Net` for contracts and loopback transports. Use `Opus.Net.Udp` for
UDP.

When changing transport behavior:

1. Update contract tests when observable behavior changes.
2. Add loopback coverage if higher layers can test against the contract.
3. Add UDP frame codec tests for wire changes.
4. Add integration tests for heartbeat, deadline, disconnect, queue limits, or
   rate limits.
5. Keep timing tests bounded and deterministic where possible.

## Common Problems

### Build sees old API after a file sync

Run:

```powershell
dotnet build .\OpusEngine.sln -c Release -t:Rebuild
```

This clears stale incremental references without deleting source-side build
scripts or known-good profiles.

### D3D12 device creation fails

Check:

- Windows host;
- compatible adapter;
- graphics driver;
- DXC availability;
- whether non-D3D12 tests pass;
- whether the failure is in PAL window setup, RHI device creation, swap chain,
  or renderer resource setup.

### Package validation reports path errors

Check:

- package-relative paths;
- no absolute paths in the manifest;
- no parent-directory traversal;
- file size and SHA-256 hash match the manifest;
- asset type matches the declared file.

### UDP integration test flakes

Check:

- test timing options;
- whether tests are running in parallel;
- whether ports are available;
- whether the assertion can observe an event list instead of sleeping blindly.

### Consumer assembly fails to load

Check:

- path resolves;
- file is a managed assembly;
- dependencies can resolve;
- exactly one suitable factory is visible;
- factory has a public parameterless constructor;
- `CreateIntegration()` returns a valid facade.

### Editor opens but no model appears

Check:

- `--content-root` or project content roots;
- whether the model browser lists the asset;
- whether `inspect <model>` can read the glTF/GLB;
- whether the node is hidden;
- whether the camera needs `F` to frame the scene.

## Review Checklist

Before considering a change done:

- the touched project builds;
- the closest test project passes;
- a neighboring layer test passes if the change crosses a boundary;
- public contracts have tests;
- backend behavior has backend tests;
- CLI output changes have command tests;
- diagnostics use stable codes;
- generated output is not part of the change;
- D3D12-only code stays in D3D12 projects;
- platform-only code stays in PAL projects;
- editor document mutations are undoable;
- docs and command examples match the current CLI.
