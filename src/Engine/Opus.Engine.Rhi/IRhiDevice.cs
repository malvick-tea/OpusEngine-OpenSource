using System;

namespace Opus.Engine.Rhi;

/// <summary>
/// The GPU device — the root handle from which every other RHI resource is created.
/// One per process at runtime; tests may spin up <see cref="RhiBackendKind.Null"/>
/// devices in parallel.
///
/// Disposal releases the underlying native device + all live resources it owns.
/// Renderer / FrameGraph layers must finish in-flight frame work before disposal.
///
/// Allocation contract: factory methods (CreateBuffer / CreateTexture / etc.) may
/// allocate native memory + return ref-counted handles. Dispose on the handle decrements
/// the refcount; the device defers actual release until the GPU has finished using the
/// resource (tracked via per-resource fence values). Until R-1's live backend lands,
/// the Null device returns no-op handles.
/// </summary>
public interface IRhiDevice : IDisposable
{
    RhiBackendKind Backend { get; }

    RhiCapabilities Capabilities { get; }

    /// <summary>Display name of the underlying adapter (e.g. "NVIDIA GeForce RTX 4070")
    /// for diagnostics / banner / crash-dump headers. Implementation-defined for Null.</summary>
    string AdapterName { get; }

    IRhiCommandList CreateCommandList(string debugName);

    IRhiBuffer CreateBuffer(RhiBufferDescription description);

    IRhiTexture CreateTexture(RhiTextureDescription description);

    IRhiShader CreateShader(RhiShaderDescription description);

    IRhiPipeline CreatePipeline(RhiPipelineDescription description);

    /// <summary>Blocks until every previously-submitted command list has completed
    /// on the GPU. Used at shutdown and at swap-chain recreation. Not for hot-path use.</summary>
    void WaitForIdle();
}
