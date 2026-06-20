# Consumer Integration

Consumer integration is the boundary between the Opus alpha host and an external
application module. The host owns windowing, timing, diagnostics, rendering, and
shutdown. A consumer assembly can provide lifecycle hooks, scene frames, asset
resolution, and telemetry through engine contracts.

This keeps the engine runnable without hardcoding one game or one application.

## Main Projects

```text
src/Engine/Opus.Engine.Consumer
src/Apps/Opus.App.OpusAlpha
src/Engine/Opus.Engine.Host.Windows.Direct3D12
```

`Opus.Engine.Consumer` defines the contracts.

`Opus.App.OpusAlpha` loads an external assembly and constructs the integration
facade.

`Opus.Engine.Host.Windows.Direct3D12` adapts consumer scene, asset, lifecycle,
and telemetry data into the live D3D12 host.

## Contracts

Important types:

- `IConsumerIntegrationFactory`
- `ConsumerIntegration`
- `ConsumerContractValidation`
- `ConsumerDiagnosticCodes`
- `IConsumerLifecycleHook`
- `ConsumerLifecycleStartedContext`
- `ConsumerLifecycleStoppingContext`
- `ConsumerFrameContext`
- `IConsumerSceneSource`
- `ConsumerSceneFrame`
- `ConsumerCameraSet`
- `ConsumerLightingSnapshot`
- `ConsumerDrawItem`
- `IConsumerAssetCatalog`
- `ConsumerAssetId`
- `ConsumerAssetRequest`
- `ConsumerAssetResolution`
- `IConsumerTelemetryProvider`
- `ConsumerTelemetrySnapshot`

A consumer does not receive ownership of raw D3D12 resources. It describes data;
the host decides how to adapt that data to the current backend.

## Load Flow

Run the alpha host with a consumer assembly:

```powershell
$env:OPUS_CONSUMER_TRUST_KEY = "C:\keys\consumer-publisher-public.pem"
dotnet run -c Release --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --consumer <path-to-assembly>
```

The public key must be P-256. The assembly must have a sibling
`<path-to-assembly>.sig` containing its ECDSA signature. Verification reads the
assembly once and the load context consumes those exact verified bytes. The
consumer context is collectible, rejects private managed dependencies, and
denies unmanaged DLL resolution.

Load flow:

```text
ConsumerIntegrationAssemblyLoader
  -> resolve full path
  -> read assembly and signature under bounded sizes
  -> verify ECDSA P-256 signature
  -> ConsumerPluginLoadContext
  -> load verified bytes
  -> get loadable types
  -> ConsumerIntegrationFactoryResolver
  -> create factory
  -> factory.CreateIntegration()
  -> validate ConsumerIntegration
```

Common boundary failures return `ConsumerIntegrationLoadResult` instead of
escaping as raw exceptions.

## Factory Rules

A consumer assembly should expose exactly one public, concrete implementation of
`IConsumerIntegrationFactory`.

The implementation must:

- be public;
- be non-abstract;
- have a public parameterless constructor;
- return a non-null `ConsumerIntegration` from `CreateIntegration()`.

Zero factories, multiple factories, constructor failures, and null integrations
are reported as load failures.

## Integration Facade

`ConsumerIntegration` groups the optional consumer parts:

- `IConsumerSceneSource?`
- `IConsumerAssetCatalog?`
- `IConsumerTelemetryProvider?`
- `IReadOnlyList<IConsumerLifecycleHook>`

Keep each part focused:

- scene sources describe frames;
- asset catalogs resolve asset ids;
- telemetry providers expose cheap snapshots;
- lifecycle hooks react to host start/frame/stop events.

Do not use one contract to smuggle another responsibility. Scene generation
should not parse CLI options. Telemetry should not block on IO. Asset lookup
should not mutate simulation state.

## Minimal Factory

```csharp
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Consumer.Lifecycle;

public sealed class SampleConsumerFactory : IConsumerIntegrationFactory
{
    public ConsumerIntegration CreateIntegration()
    {
        return new ConsumerIntegration(
            sceneSource: new SampleSceneSource(),
            assetCatalog: new SampleAssets(),
            telemetryProvider: new SampleTelemetry(),
            lifecycleHooks: new IConsumerLifecycleHook[]
            {
                new SampleLifecycle(),
            });
    }
}
```

Use the constructor shape from the current source. If `ConsumerIntegration`
changes, update factories, fixture tests, and this document together.

## Lifecycle Hooks

Lifecycle hooks let a consumer observe host events:

- host started;
- per-frame callback;
- host stopping.

Use them for consumer-owned state. Do not use them to take over the host loop;
`OpusHost` owns timing.

## Scene Source

`IConsumerSceneSource` provides scene frames to the host.

Scene data can include:

- camera data;
- lighting;
- draw items;
- asset ids or paths;
- per-frame transform/tint data.

The scene source should describe the frame. It should not record D3D12 commands.

## Asset Catalog

`IConsumerAssetCatalog` resolves asset requests.

Use stable asset ids. Return clear failures for missing assets. Keep resolution
separate from renderer upload; the host and renderer decide how resolved assets
become GPU resources.

## Telemetry

Telemetry contracts let a consumer expose a small state snapshot to diagnostics.

Good telemetry is:

- cheap to query;
- non-blocking;
- stable enough to show in overlays and failure reports;
- explicit about missing or unavailable data.

## Testing A Consumer Module

Recommended tests:

- factory is discoverable;
- factory returns a valid integration;
- lifecycle hooks handle start/frame/stop;
- scene source returns stable frame data;
- asset catalog resolves known ids and rejects unknown ids;
- telemetry is cheap and does not throw;
- alpha host loader reports clear failures for malformed assemblies.

The repository includes a test-only consumer plugin fixture under:

```text
src/Apps/Opus.App.OpusAlpha.Tests.ConsumerPluginFixture
```

## Debugging Load Failures

Check in this order:

1. The path resolves.
2. The file is a managed assembly.
3. The assembly dependencies can resolve.
4. A factory type is loadable.
5. Exactly one suitable factory is selected.
6. The factory has a public parameterless constructor.
7. `CreateIntegration()` succeeds.
8. The returned integration passes contract validation.

If reflection cannot load every type, the loader rejects the plugin. A partially
resolved assembly is not allowed to execute through a subset of its declared
types.

## Changing The Contract

When changing `Opus.Engine.Consumer`:

1. Update the contract type.
2. Update contract validation.
3. Update alpha host loading or D3D12 adaptation if discovery or data flow
   changes.
4. Update fixture consumer assemblies.
5. Update tests for valid and invalid integrations.
6. Update this document.

Prefer additive changes where possible. If a contract becomes required, add a
clear diagnostic for missing implementations.

## Review Checklist

- Consumer contracts do not expose backend-owned resources unnecessarily.
- The loader returns actionable failures.
- Reflection handles partially loadable assemblies.
- The factory resolver rejects ambiguous factories.
- Lifecycle, scene, assets, and telemetry stay separate.
- Tests cover valid and invalid consumer assemblies.
- Alpha host behavior remains valid without a consumer assembly.
