using System;
using Opus.Engine.FrameGraph;
using Opus.Engine.Rhi;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Resource registration + pass registration surface of the frame graph: callers
/// hand owned / non-owning textures via <see cref="ImportTexture"/> /
/// <see cref="ImportNativeTexture"/>, request post-frame state via
/// <see cref="EnsureFinalState"/>, and append passes via <see cref="AddPass"/>. Each call
/// invalidates the compiled plan so the next <c>Compile</c> rebuilds barriers from the
/// new declarations.</summary>
public sealed unsafe partial class D3D12FrameGraph
{
    /// <summary>Registers a caller-owned texture for use as a frame-graph resource.
    /// The graph does not release the texture's native handle on dispose — caller
    /// keeps the lifetime responsibility. <paramref name="initialState"/> tells the
    /// graph what D3D12 state the resource is in *before* the first pass runs so
    /// auto-barrier planning starts from the correct baseline.</summary>
    public FrameGraphResource ImportTexture(D3D12Texture texture, ResourceStates initialState = ResourceStates.Common)
    {
        if (texture == null)
        {
            throw new ArgumentNullException(nameof(texture));
        }

        var handle = new FrameGraphResource(_nextHandleId++, FrameGraphResourceKind.Texture);
        EnsureSlot(_imports, handle.Id);
        _imports[handle.Id] = texture;
        _importInitialStates[handle.Id] = initialState;
        _compiled = false;
        return handle;
    }

    /// <summary>Convenience overload: wraps the raw native pointer in a non-owning
    /// <see cref="D3D12Texture"/> for the duration of the frame. Internally creates a
    /// wrapper that's disposed on <see cref="Reset"/>. Use this for swap-chain back
    /// buffers which can't be wrapped persistently (the back-buffer index rotates).</summary>
    public FrameGraphResource ImportNativeTexture(
        string debugName,
        ID3D12Resource* native,
        int width,
        int height,
        Format dxgiFormat,
        ResourceStates initialState = ResourceStates.Common,
        RhiTextureFormat abstractFormat = RhiTextureFormat.Rgba8Unorm,
        RhiTextureUsage usage = RhiTextureUsage.ColorTarget)
    {
        var wrapper = D3D12Texture.WrapNonOwning(debugName, native, width, height, dxgiFormat, abstractFormat, usage);
        _wrappersToDispose.Add(wrapper);
        return ImportTexture(wrapper, initialState);
    }

    /// <summary>Requests that <paramref name="handle"/> be transitioned to
    /// <paramref name="state"/> after the last pass executes — typical use is taking the
    /// swap-chain back buffer back to <c>Present</c> at end-of-frame.</summary>
    public void EnsureFinalState(FrameGraphResource handle, ResourceStates state)
    {
        if (handle.Kind != FrameGraphResourceKind.Texture)
        {
            throw new InvalidOperationException("EnsureFinalState supports texture handles only in R-5.");
        }

        _finalStates[handle.Id] = state;
        _compiled = false;
    }

    public void AddPass(D3D12RenderPass pass)
    {
        if (pass == null)
        {
            throw new ArgumentNullException(nameof(pass));
        }

        var passIndex = _passes.Count;
        _passes.Add(pass);
        var usages = new System.Collections.Generic.List<ResourceUsageDeclaration>();
        _passUsages[passIndex] = usages;

        var builder = new D3D12FrameGraphBuilder(usages);
        pass.Setup(builder);

        _compiled = false;
    }
}
