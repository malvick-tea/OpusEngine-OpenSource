using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Resource-state transition recording. Every resource handed to the GPU has
/// a current state (RenderTarget, PixelShaderResource, UnorderedAccess, etc.); the
/// driver requires the app to bracket reads / writes with transitions to the matching
/// state. Three overloads cover the common shapes: raw <c>ID3D12Resource*</c>,
/// <see cref="D3D12Texture"/>, <see cref="D3D12Buffer"/>; the
/// <see cref="ResourceBarrierTransitionSubresource"/> variant targets a single mip /
/// array slice for Hi-Z and other ping-pong patterns.</summary>
public sealed unsafe partial class D3D12CommandList
{
    /// <summary>Inserts a single resource state transition over every subresource. The
    /// minimum recording API needed for swap chain back-buffer clear-and-present.</summary>
    public void ResourceBarrierTransition(
        ID3D12Resource* resource,
        ResourceStates before,
        ResourceStates after)
    {
        var barrier = new ResourceBarrier
        {
            Type = ResourceBarrierType.Transition,
            Flags = ResourceBarrierFlags.None,
            Anonymous = new ResourceBarrierUnion
            {
                Transition = new ResourceTransitionBarrier
                {
                    PResource = resource,
                    StateBefore = before,
                    StateAfter = after,
                    Subresource = uint.MaxValue, // D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES
                },
            },
        };

        _commandList->ResourceBarrier(1u, &barrier);
    }

    /// <summary>Resource barrier transition for a buffer. Default-heap buffers participate
    /// in state transitions just like textures (Upload-heap buffers stay in GenericRead
    /// always and shouldn't be transitioned).</summary>
    public void ResourceBarrierTransition(D3D12Buffer buffer, ResourceStates before, ResourceStates after) =>
        ResourceBarrierTransition(buffer.Native, before, after);

    /// <summary>Resource barrier transition for a texture by reference (cleaner call-site
    /// than the raw <see cref="ResourceBarrierTransition(ID3D12Resource*, ResourceStates, ResourceStates)"/>).</summary>
    public void ResourceBarrierTransition(D3D12Texture texture, ResourceStates before, ResourceStates after) =>
        ResourceBarrierTransition(texture.Native, before, after);

    /// <summary>Per-subresource transition. <paramref name="subresourceIndex"/> for a
    /// non-array 2D texture is the mip slice; for cubemaps and arrays use
    /// <c>arraySlice * mipLevels + mipSlice</c>. R-17.b Hi-Z generation transitions each
    /// mip in isolation as it ping-pongs UAV writes → SRV reads.</summary>
    public void ResourceBarrierTransitionSubresource(D3D12Texture texture, ResourceStates before, ResourceStates after, uint subresourceIndex)
    {
        var barrier = new ResourceBarrier
        {
            Type = ResourceBarrierType.Transition,
            Flags = ResourceBarrierFlags.None,
            Anonymous = new ResourceBarrierUnion
            {
                Transition = new ResourceTransitionBarrier
                {
                    PResource = texture.Native,
                    StateBefore = before,
                    StateAfter = after,
                    Subresource = subresourceIndex,
                },
            },
        };
        _commandList->ResourceBarrier(1u, &barrier);
    }
}
