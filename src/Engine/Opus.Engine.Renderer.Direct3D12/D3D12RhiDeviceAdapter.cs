using System;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12;

/// <summary>
/// <see cref="IRhiDevice"/> facade over the concrete <see cref="D3D12RhiDevice"/>. Exposes
/// the safe subset (Backend / Capabilities / AdapterName / WaitForIdle) that the abstract
/// surface requires; factory methods throw <see cref="NotSupportedException"/> until the
/// abstract <c>IRhiBuffer</c>/<c>IRhiTexture</c>/<c>IRhiShader</c>/<c>IRhiPipeline</c>
/// descriptions are mapped end-to-end to D3D12 (planned post-M3 once Renderer code starts
/// allocating GPU resources through the abstract API).
/// </summary>
/// <remarks>
/// Bridges <see cref="D3D12RhiDevice"/> (concrete, has typed factory surface like
/// <c>CreateGraphicsBuffer(RhiBufferDescription) → D3D12Buffer</c>) and
/// <see cref="IRhiDevice"/> (abstract, has erased factory surface like
/// <c>CreateBuffer(RhiBufferDescription) → IRhiBuffer</c>). The D3D12 buffer/texture/etc.
/// types don't implement the abstract interfaces today (they predate them); the bridge is
/// scoped to M3-wrap.a — production passes still go through
/// <see cref="D3D12Renderer.DeviceConcrete"/> for now.
/// </remarks>
internal sealed class D3D12RhiDeviceAdapter : IRhiDevice
{
    private readonly D3D12RhiDevice _concrete;

    public D3D12RhiDeviceAdapter(D3D12RhiDevice concrete)
    {
        _concrete = concrete ?? throw new ArgumentNullException(nameof(concrete));
    }

    public RhiBackendKind Backend => RhiBackendKind.D3D12;

    /// <summary>Full feature mask — Direct3D 12 on a modern Windows 11 / RTX-class adapter
    /// supports every capability flag the renderer queries. Refined per-adapter when the
    /// concrete device exposes a real capability probe in a follow-up milestone.</summary>
    public RhiCapabilities Capabilities =>
        RhiCapabilities.MeshShaders
        | RhiCapabilities.RayTracing
        | RhiCapabilities.AsyncCompute
        | RhiCapabilities.AsyncCopy
        | RhiCapabilities.DynamicResources
        | RhiCapabilities.VariableRateShading
        | RhiCapabilities.WaveIntrinsics;

    public string AdapterName => _concrete.AdapterName;

    public IRhiCommandList CreateCommandList(string debugName) =>
        throw NotMappedYet(nameof(CreateCommandList));

    public IRhiBuffer CreateBuffer(RhiBufferDescription description) =>
        throw NotMappedYet(nameof(CreateBuffer));

    public IRhiTexture CreateTexture(RhiTextureDescription description) =>
        throw NotMappedYet(nameof(CreateTexture));

    public IRhiShader CreateShader(RhiShaderDescription description) =>
        throw NotMappedYet(nameof(CreateShader));

    public IRhiPipeline CreatePipeline(RhiPipelineDescription description) =>
        throw NotMappedYet(nameof(CreatePipeline));

    public void WaitForIdle() => _concrete.WaitForIdle();

    public void Dispose()
    {
        // The concrete device is owned by the test harness / hosting process — not by the
        // adapter, not by the renderer that holds it. Adapter disposal is a no-op.
    }

    private static NotSupportedException NotMappedYet(string memberName) =>
        new(
            $"{nameof(D3D12RhiDeviceAdapter)}.{memberName}: abstract IRhi* resource creation is not yet bridged to D3D12 "
            + "(M3-wrap.a ships the renderer skeleton; resource creation lands when ForwardPlusSceneRenderer needs it). "
            + "For D3D12-specific factory calls, use D3D12Renderer.DeviceConcrete.");
}
