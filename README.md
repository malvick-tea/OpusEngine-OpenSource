# OpusOpenSource

OpusOpenSource is the engine-side codebase used by STO and by the Opus alpha
host. It contains runtime hosting, platform abstractions, rendering interfaces,
D3D12 backend code, UI drawing, content loading and packaging, networking,
persistence, localisation, diagnostics, developer tools, and tests.

## Project State

This is not a finished engine. It is a source snapshot from the period when Opus
was approaching its first alpha. Some features are incomplete, some APIs may
change, and bugs should be expected. Treat the code as a working foundation for
study, experimentation, and continued development rather than a complete engine.

See [`CONTRIBUTORS.md`](CONTRIBUTORS.md) for a summary of contributor work.

The engine is intentionally split into small assemblies. Most projects expose a
single responsibility: a contract layer, one backend, a host adapter, a validator,
or a focused set of tests. This makes it possible to work on content tools,
networking, simulation hosts, and renderer code without pulling the entire engine
into every change.

## Main Ideas

Opus is built around a few recurring boundaries:

- Runtime host vs application callbacks.
- Platform abstraction vs platform backend.
- Renderer contracts vs D3D12 implementation.
- Frame graph pass declarations vs D3D12 command recording.
- Content parsing vs package validation.
- Transport contracts vs UDP and loopback transports.
- UI interfaces vs D3D12 draw-surface implementation.
- Engine contracts vs consumer game integration.

The project is not a single monolith. Read it as a set of cooperating modules.

## Runtime Flow

The minimal runtime loop is `OpusHost`:

```text
OpusHost.Start()
  -> application.OnStarted(context)

OpusHost.Step(frameDelta)
  -> window.PollEvents()
  -> fixed ticks while accumulator permits
  -> application.FixedTick(time)
  -> application.Render(renderFrame)

OpusHost.Stop()
  -> application.OnStopping(context)
  -> window.Close()
```

The host owns deterministic fixed ticks and variable render callbacks. The
application supplies behavior through `IOpusApplication`.

## Engine Areas

### Foundation

Project:

```text
src/Foundation/Opus.Foundation
```

Foundation contains shared primitives used by almost every other assembly:

- result and error types;
- guard helpers;
- version and build identity;
- geometry primitives such as AABB and frustum;
- logging contracts and rolling log sinks;
- engine exception types for content and device failure boundaries.

Keep Foundation small. If a type knows about rendering, networking, or content
formats, it probably belongs elsewhere.

### Runtime

Project:

```text
src/Engine/Opus.Engine.Runtime
```

Runtime contains `OpusHost`, `IOpusApplication`, host options, host state, and
render-frame timing data. It coordinates lifecycle and timing without owning a
renderer.

### PAL

Projects:

```text
src/Engine/Opus.Engine.Pal
src/Engine/Opus.Engine.Pal.Sdl3
src/Engine/Opus.Engine.Pal.Windows
src/Engine/Opus.Engine.Pal.Windows.Direct3D12
```

PAL stands for platform abstraction layer. It defines and implements:

- window services;
- lifecycle services;
- virtual file systems;
- platform-specific window/session helpers;
- SDL3-backed input/window services;
- Windows and D3D12 host glue.

PAL code is the place for OS and window-system details.

### Input

Projects:

```text
src/Engine/Opus.Engine.Input
src/Engine/Opus.Engine.Input.Sdl3
```

The input contract is backend-agnostic. SDL3 provides one implementation. Client
code should depend on the input interfaces rather than SDL types.

### RHI And D3D12 Backend

Projects:

```text
src/Engine/Opus.Engine.Rhi
src/Engine/Opus.Engine.Rhi.D3D12
```

`Opus.Engine.Rhi` defines the rendering hardware interface contracts:

- devices;
- buffers;
- textures;
- command lists;
- capabilities;
- resource descriptions.

`Opus.Engine.Rhi.D3D12` implements those contracts and adds D3D12-specific
building blocks:

- device and adapter selection;
- descriptor heaps;
- upload paths;
- swap chain;
- command lists;
- root signatures;
- pipeline factories;
- texture readback and screenshots;
- D3D12 frame graph.

### Frame Graph

Projects:

```text
src/Engine/Opus.Engine.FrameGraph
src/Engine/Opus.Engine.Rhi.D3D12/FrameGraph
```

The abstract frame graph defines pass and resource handles. The D3D12 frame graph
stores imported resources, pass usage declarations, barrier plans, final-state
hints, and execution order.

The frame graph is the coordination layer between high-level renderer passes and
raw command recording.

### Renderer

Projects:

```text
src/Engine/Opus.Engine.Renderer
src/Engine/Opus.Engine.Renderer.Direct3D12
```

The renderer layer contains camera and lighting structures, scene viewport
contracts, D3D12 scene rendering, material atlases, glTF GPU loading, instanced
draw batching, culling, LOD selection, forward scene passes, tonemapping, and
demo scene helpers.

The D3D12 scene path is centered around:

- `D3D12ForwardSceneRenderer`;
- `ForwardScenePass`;
- `ForwardSceneTargets`;
- `TonemapPass`;
- `SceneInstanceBatch`;
- `SceneNodeCuller`;
- `SceneLodSelector`;
- `D3D12GltfSceneLoader`.

### UI

Projects:

```text
src/Engine/Opus.Engine.Ui
src/Engine/Opus.Engine.Ui.Direct3D12
```

The UI contract is an immediate draw-surface model. Screens render through
`IDrawSurface`; backends provide the concrete draw surface.

The D3D12 UI backend handles:

- quad batches;
- text layout;
- font fallback;
- glyph atlas baking;
- ink stroke tessellation;
- world-space text projection.

### Content

Projects:

```text
src/Content/Opus.Content
src/Content/Opus.Content.Packaging
```

`Opus.Content` covers low-level content helpers:

- glTF/GLB reading;
- scene tree math;
- mesh data;
- animation sampling;
- texture decoding;
- mip generation;
- block compression.

`Opus.Content.Packaging` covers package manifests, package archives, validators,
signing, verification, relative path safety, and manifest generation.

### Localisation

Project:

```text
src/Localisation/Opus.Localisation
```

Localisation provides a small translation catalog contract and CSV-backed
catalog implementation. Missing keys return the key itself, which makes missing
text visible during local work.

### Networking

Projects:

```text
src/Net/Opus.Net
src/Net/Opus.Net.Udp
```

`Opus.Net` defines transport contracts and loopback transports for tests and
single-process local runs. `Opus.Net.Udp` implements UDP frames, client/server
transports, heartbeat/deadline handling, bounded queues, and per-peer inbound
rate limiting.

### Persistence

Project:

```text
src/Persistence/Opus.Persistence
```

Persistence defines binary codec contracts, MemoryPack integration, save header
framing, save-store contracts, replay headers, snapshot provider contracts, and
JSON settings serialization.

### Consumer Integration

Project:

```text
src/Engine/Opus.Engine.Consumer
```

Consumer integration is the boundary for game/application assemblies that want
to feed scene, lifecycle, telemetry, and asset data into the Opus alpha host.

Important contracts:

- `IConsumerIntegrationFactory`;
- `ConsumerIntegration`;
- `IConsumerLifecycleHook`;
- `IConsumerSceneSource`;
- `IConsumerAssetCatalog`;
- telemetry provider interfaces.

### Opus Alpha Host

Project:

```text
src/Apps/Opus.App.OpusAlpha
```

The alpha host is a command-line and window harness for engine checks. It can
open a live D3D12 window, run smoke frames, validate packages, inspect machine
capabilities, run network soak checks, run stress iterations, and load consumer
integration assemblies through `--consumer`.

## Build

Requirements:

- .NET SDK 8.
- Windows for D3D12 host projects and D3D12 tests.

From `OpusOpenSource`:

```powershell
dotnet restore .\OpusEngine.sln
dotnet build .\OpusEngine.sln
```

Run tests:

```powershell
dotnet test .\OpusEngine.sln
```

Build output goes under `build/output/`.

## Run The Alpha Host

Show help:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --help
```

Open the default window path:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj
```

Run a smoke pass:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --frames 60
```

Validate a package:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- validate-package --package <path>
```

Load a consumer assembly:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --consumer <path-to-assembly>
```

## Tools

The package validator CLI lives here:

```text
src/Tools/Opus.Tool.PackageValidator
```

Common command shape:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- <command> <options>
```

The tool supports validating, generating, packing, verifying, and unpacking Opus
content packages.

## Suggested Reading Order

If you are new to the engine:

1. `src/Engine/Opus.Engine.Runtime/OpusHost.cs`
2. `src/Engine/Opus.Engine.Ui/IDrawSurface.cs`
3. `src/Engine/Opus.Engine.Rhi/IRhiDevice.cs`
4. `src/Engine/Opus.Engine.Rhi.D3D12/FrameGraph/D3D12FrameGraph.cs`
5. `src/Engine/Opus.Engine.Renderer.Direct3D12/Scene/D3D12ForwardSceneRenderer.cs`
6. `src/Content/Opus.Content.Packaging/Validation/PackageValidator.cs`
7. `src/Net/Opus.Net/Transport/INetTransport.cs`
8. `src/Engine/Opus.Engine.Consumer/Integration/ConsumerIntegration.cs`
9. `src/Apps/Opus.App.OpusAlpha/Program.cs`

## Documentation

- [`docs/OPERATING.md`](docs/OPERATING.md) - commands, local workflows, and
  troubleshooting.
- [`docs/RENDERING.md`](docs/RENDERING.md) - RHI, frame graph, D3D12 renderer,
  UI drawing, and how to add renderer pieces.
- [`docs/CONTENT_PACKAGING.md`](docs/CONTENT_PACKAGING.md) - package manifests,
  validation, archives, signing, and the package tool.
- [`docs/CONSUMER_INTEGRATION.md`](docs/CONSUMER_INTEGRATION.md) - how external
  application modules connect to the alpha host.

## Working Rules

- Keep platform details inside PAL or backend projects.
- Keep renderer interfaces separate from D3D12 implementation.
- Keep package validation headless.
- Keep transport contracts independent from UDP details.
- Keep UI interfaces backend-agnostic.
- Keep consumer integration behind explicit contracts.
- Add tests at the same boundary where behavior is introduced.
