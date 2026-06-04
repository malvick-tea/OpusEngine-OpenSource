# Operating Guide

This guide covers local work with OpusOpenSource: build, test, run the alpha
host, use tools, and change modules safely.

## Local Requirements

- .NET SDK 8.
- Windows for D3D12 host projects and D3D12 tests.
- A graphics adapter with D3D12 support for live window and renderer checks.
- A normal system font set for UI text fallback.

Optional:

- Visual Studio or Rider for solution navigation.
- PIX, RenderDoc, or another graphics debugger for renderer work.
- A local content workspace for package and asset checks.

## First Build

```powershell
cd C:\Users\DAS\Documents\OpusOpenSource
dotnet restore .\OpusEngine.sln
dotnet build .\OpusEngine.sln
```

Generated output goes under:

```text
build/output/
```

Clean it with:

```powershell
Remove-Item -Recurse -Force .\build
```

## Test Strategy

Run all tests:

```powershell
dotnet test .\OpusEngine.sln
```

For normal development, run the closest test project first:

```powershell
dotnet test .\src\Foundation\Opus.Foundation.Tests\Opus.Foundation.Tests.csproj
dotnet test .\src\Content\Opus.Content.Tests\Opus.Content.Tests.csproj
dotnet test .\src\Content\Opus.Content.Packaging.Tests\Opus.Content.Packaging.Tests.csproj
dotnet test .\src\Engine\Opus.Engine.Renderer.Tests\Opus.Engine.Renderer.Tests.csproj
dotnet test .\src\Engine\Opus.Engine.Ui.Direct3D12.Tests\Opus.Engine.Ui.Direct3D12.Tests.csproj
dotnet test .\src\Net\Opus.Net.Udp.Tests\Opus.Net.Udp.Tests.csproj
```

Then run one neighboring layer. Example: if you change D3D12 scene rendering,
run renderer tests and UI/D3D12 tests. If you change package validation, run
packaging tests and package tool tests.

## Build By Area

Foundation:

```powershell
dotnet build .\src\Foundation\Opus.Foundation\Opus.Foundation.csproj
```

Runtime:

```powershell
dotnet build .\src\Engine\Opus.Engine.Runtime\Opus.Engine.Runtime.csproj
```

RHI D3D12:

```powershell
dotnet build .\src\Engine\Opus.Engine.Rhi.D3D12\Opus.Engine.Rhi.D3D12.csproj
```

Renderer D3D12:

```powershell
dotnet build .\src\Engine\Opus.Engine.Renderer.Direct3D12\Opus.Engine.Renderer.Direct3D12.csproj
```

Package tool:

```powershell
dotnet build .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj
```

Alpha host:

```powershell
dotnet build .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj
```

## Run The Alpha Host

Show help:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --help
```

Open the default live window:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj
```

Open a supplied asset:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- <path-to-glb-or-gltf>
```

Run a smoke check:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --frames 60
```

Capture a smoke screenshot:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --frames 60 --screenshot-frame 30
```

Use a diagnostics directory:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- smoke --diagnostics-dir .\.local\diagnostics
```

Keep diagnostics output outside source changes unless you are adding a tiny
fixture for a test.

## Alpha Host Modes

The alpha host supports:

- default live D3D12 window;
- asset-backed window run;
- `smoke`;
- `validate-package`;
- `check-machine`;
- `soak`;
- `stress`;
- known-issue ledger merge and diff;
- consumer assembly loading through `--consumer`.

Read the generated help text for exact flags:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --help
```

## Package Tool

Tool project:

```text
src/Tools/Opus.Tool.PackageValidator
```

Show help:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- --help
```

Validate a package directory:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- validate --package <path>
```

Generate a manifest:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- generate --package <path>
```

Pack a package:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- pack --package <path> --output <path>
```

Verify an archive:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- verify --archive <path>
```

Unpack an archive:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- unpack --archive <path> --output <dir>
```

If a command fails, first check whether the path points at a package directory,
manifest, or archive as expected by that command.

## Runtime Host Workflow

Use `OpusHost` when adding or testing application lifecycle behavior.

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

- Fixed-tick behavior belongs in `FixedTick`.
- Presentation belongs in `Render`.
- Window close and lifecycle shutdown request host shutdown.
- `MaxFixedTicksPerFrame` prevents an overloaded frame from draining an unlimited
  backlog.
- `DroppedFixedTime` is observable for tests and diagnostics.

## PAL Workflow

Use PAL projects for OS, window, VFS, lifecycle, and platform service work.

Rules:

- Shared contracts go in `Opus.Engine.Pal`.
- SDL3-specific code goes in `Opus.Engine.Pal.Sdl3` or
  `Opus.Engine.Input.Sdl3`.
- Windows filesystem and platform code goes in `Opus.Engine.Pal.Windows`.
- D3D12 window/session glue goes in `Opus.Engine.Pal.Windows.Direct3D12`.
- Engine runtime and renderer code should not know about raw OS APIs unless they
  are in a backend project.

## RHI And Frame Graph Workflow

Use `Opus.Engine.Rhi` for API-level contracts. Use `Opus.Engine.Rhi.D3D12` for
backend implementation.

When adding a D3D12 resource operation:

1. Decide whether a backend-agnostic contract is needed.
2. Add or extend the RHI interface only if more than one backend should see it.
3. Implement the D3D12 path in the matching partial or helper.
4. Add tests around state transitions, descriptors, or readback behavior.
5. Keep raw D3D12 handles out of higher-level renderer code unless the type is
   explicitly D3D12-specific.

When adding a frame graph pass:

1. Import or create the resources the pass needs.
2. Declare usage through the D3D12 frame graph builder where applicable.
3. Add the pass.
4. Ensure final resource states.
5. Test the barrier plan when the pass changes resource state.

## Renderer Workflow

Renderer changes usually touch one of these areas:

- scene loading;
- material atlas building;
- instance batching;
- culling;
- LOD selection;
- forward pass;
- tonemap pass;
- scene viewport rendering;
- demo scene helpers.

Recommended order:

1. Add pure data or planning code first.
2. Test planning without a GPU where possible.
3. Add D3D12 resource allocation.
4. Add render pass wiring.
5. Add a smoke or screenshot check only after pure tests exist.

## UI Workflow

The UI contract is `IDrawSurface`. Screens draw through the contract. The D3D12
backend converts those commands into quad batches, glyph runs, and atlas usage.

When adding UI behavior:

1. Add state to the screen or a small model type.
2. Add layout calculation.
3. Add rendering through `IDrawSurface`.
4. Test geometry or draw commands without D3D12.
5. Add D3D12 tests only when backend behavior changes.

Font behavior uses host font fallback in this source tree. Tests should assert
that a usable face can be resolved, not that one specific bundled file exists.

## Content And Package Workflow

For low-level content readers:

1. Add parsing code in `Opus.Content`.
2. Keep renderer allocation out of parsers.
3. Test with small byte arrays or small generated assets.
4. Return structured errors where possible.

For package behavior:

1. Add manifest or archive types in `Opus.Content.Packaging`.
2. Add validation in the validation area.
3. Add diagnostics with stable codes.
4. Add CLI behavior in `Opus.Tool.PackageValidator` only after library behavior
   is tested.
5. Test both text and JSON reports if output changes.

## Networking Workflow

Use `Opus.Net` for contracts and loopback transports. Use `Opus.Net.Udp` for UDP.

When changing transport behavior:

1. Update contract tests when observable behavior changes.
2. Add loopback coverage if higher layers can test against the contract.
3. Add UDP frame codec tests for wire changes.
4. Add integration tests for heartbeat, deadline, disconnect, queue limits, or
   rate limits.
5. Keep timing tests bounded and deterministic where possible.

## Persistence Workflow

Persistence owns bytes-in/bytes-out framing and codec behavior. Platform file IO
belongs in PAL implementations.

When adding a save or settings type:

1. Define the data type.
2. Use the binary codec or JSON settings serializer as appropriate.
3. Add header/framing tests.
4. Test file-store behavior through a PAL-backed store only when filesystem
   behavior is the subject.

## Consumer Integration Workflow

Consumer integration lets an external assembly feed scene, asset, lifecycle, and
telemetry data into the alpha host.

To load one:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --consumer <path-to-assembly>
```

The loader:

1. resolves the path;
2. loads the assembly through a plugin load context;
3. scans loadable types;
4. finds an `IConsumerIntegrationFactory`;
5. constructs a `ConsumerIntegration`;
6. returns structured failure text instead of throwing for common boundary
   failures.

See [`CONSUMER_INTEGRATION.md`](CONSUMER_INTEGRATION.md) for details.

## Common Problems

### D3D12 device creation fails

Check:

- Windows host.
- Compatible adapter.
- Graphics driver.
- Whether a non-D3D12 test passes.
- Whether the failure is in PAL window setup, RHI device creation, or renderer
  resource setup.

### Package validation reports path errors

Check:

- Package-relative paths.
- No absolute paths in the manifest.
- No parent-directory traversal.
- File size and hash match the manifest.
- Asset type matches the declared file.

### UDP integration test flakes

Check:

- Timing options in the test.
- Whether tests are running in parallel.
- Whether ports are available.
- Whether the assertion can observe an event list instead of sleeping blindly.

### Consumer assembly fails to load

Check:

- Path resolves to a managed assembly.
- Assembly dependencies can be resolved.
- Exactly one suitable factory is visible.
- Factory construction does not throw.

### UI text is missing

Check:

- Host font fallback.
- Glyph atlas capacity.
- Requested codepoint bands.
- Text layout bounds.

## Review Checklist

Before considering an engine change done:

- The touched project builds.
- The closest test project passes.
- A neighboring layer test passes if the change crosses a boundary.
- Public contracts have tests.
- Backend behavior has backend tests.
- CLI output changes have command tests.
- Diagnostics use stable codes.
- Generated output is not part of the change.
- D3D12-only code stays in D3D12 projects.
- Platform-only code stays in PAL projects.
