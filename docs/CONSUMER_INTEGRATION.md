# Consumer Integration

Consumer integration is the Opus boundary for an external application module.
The alpha host can load a consumer assembly, discover its factory, construct an
integration object, and ask it for lifecycle hooks, scene data, assets, and
telemetry.

## Why This Boundary Exists

The engine should be able to run its host and renderer checks without hardcoding
one game. A consumer module lets an application provide data and behavior through
contracts while the host keeps control of windowing, timing, diagnostics, and
rendering.

## Main Projects

```text
src/Engine/Opus.Engine.Consumer
src/Apps/Opus.App.OpusAlpha
```

`Opus.Engine.Consumer` defines the contracts.

`Opus.App.OpusAlpha` loads an external assembly and drives it through the alpha
host.

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

A consumer does not receive raw D3D12 ownership. It describes what it wants the
host to render or load through engine contracts.

## Load Flow

The alpha host accepts:

```powershell
dotnet run --project .\src\Apps\Opus.App.OpusAlpha\Opus.App.OpusAlpha.csproj -- --consumer <path-to-assembly>
```

Load flow:

```text
ConsumerIntegrationAssemblyLoader
  -> resolve full path
  -> ConsumerPluginLoadContext
  -> load assembly
  -> get loadable types
  -> ConsumerIntegrationFactoryResolver
  -> create factory
  -> create ConsumerIntegration
  -> validate contract
```

Boundary failures return `ConsumerIntegrationLoadResult` rather than escaping as
raw exceptions in normal cases.

## Factory Rules

A consumer assembly should expose one suitable factory implementing
`IConsumerIntegrationFactory`.

The factory should:

- be discoverable through reflection;
- construct quickly;
- avoid broad side effects in its constructor;
- return a complete `ConsumerIntegration`;
- report missing application resources through the integration boundary rather
  than through host-specific assumptions.

## Integration Object

`ConsumerIntegration` groups optional and required consumer pieces. Use it to
provide:

- lifecycle hooks;
- scene source;
- asset catalog;
- telemetry provider.

Keep each part focused. Scene generation should not parse command-line options.
Telemetry should not own renderer resources. Asset lookup should not mutate
simulation state.

## Lifecycle Hooks

Lifecycle hooks let the consumer react to host events.

Common uses:

- initialize consumer-side state after host services are ready;
- update per-frame state;
- clean up consumer-owned resources before shutdown.

Do not use lifecycle hooks to take over the host loop. The host owns timing.

## Scene Source

`IConsumerSceneSource` provides scene frames to the host.

Scene frame data includes:

- camera;
- lighting;
- draw items;
- viewport-related values.

The scene source should describe the frame. It should not record D3D12 commands.

## Asset Catalog

`IConsumerAssetCatalog` resolves asset requests.

Use stable asset IDs. Return clear failures for missing assets. Keep resolution
separate from renderer upload; the host and renderer decide how assets become GPU
resources.

## Telemetry

Telemetry contracts let a consumer expose a small state snapshot to host
diagnostics. Keep telemetry cheap to query and avoid hidden blocking IO.

## Adding A Consumer Module

1. Create a class library that references `Opus.Engine.Consumer`.
2. Implement `IConsumerIntegrationFactory`.
3. Return a `ConsumerIntegration`.
4. Add a lifecycle hook if the module needs start/stop/frame callbacks.
5. Add a scene source if the module wants to render scene data.
6. Add an asset catalog if the scene refers to assets.
7. Add a telemetry provider if host diagnostics should display module state.
8. Build the assembly.
9. Run the alpha host with `--consumer <assembly>`.

## Minimal Factory Shape

```csharp
using Opus.Engine.Consumer.Integration;

public sealed class SampleConsumerFactory : IConsumerIntegrationFactory
{
    public ConsumerIntegration Create()
    {
        return new ConsumerIntegration(
            lifecycle: new SampleLifecycle(),
            sceneSource: new SampleSceneSource(),
            assetCatalog: new SampleAssets(),
            telemetryProvider: new SampleTelemetry());
    }
}
```

Use the exact constructor shape exposed by the current `ConsumerIntegration`
type. If that type changes, update factories and tests together.

## Testing A Consumer Module

Recommended tests:

- factory is discoverable;
- factory returns a valid integration;
- lifecycle hook handles start/stop once;
- scene source returns stable frame data;
- asset catalog resolves known IDs and rejects unknown IDs;
- telemetry is cheap and does not throw;
- alpha host loader reports a clear failure for malformed assemblies.

The Opus repo includes consumer fixture tests under the alpha host test area.

## Debugging Load Failures

Check in this order:

1. The path resolves.
2. The file is a managed assembly.
3. The assembly dependencies can resolve.
4. A factory type is loadable.
5. Exactly one suitable factory is selected.
6. Factory construction succeeds.
7. The returned integration passes contract validation.

If reflection cannot load every type, the loader keeps the types that did load.
This allows a valid factory to be found even when unrelated types in the assembly
have missing dependencies.

## Changing The Contract

When changing `Opus.Engine.Consumer`:

1. Update the contract type.
2. Update contract validation.
3. Update the alpha host loader or resolver if discovery changes.
4. Update fixture consumer assemblies.
5. Update tests for valid and invalid integrations.
6. Update this document if the workflow changes.

Prefer additive changes where possible. If a contract becomes required, add a
clear diagnostic for missing implementations.

## Review Checklist

- Consumer contracts do not expose backend-owned resources unnecessarily.
- The loader returns actionable failures.
- Reflection code handles partially loadable assemblies.
- The factory resolver rejects ambiguous factories.
- Lifecycle, scene, assets, and telemetry stay separate.
- Tests cover both valid and invalid consumer assemblies.
