using Opus.Engine.FrameGraph;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Per-pass execution context handed to <see cref="D3D12RenderPass.Execute"/>. Provides
/// the shared <see cref="D3D12CommandList"/> + resolves <see cref="FrameGraphResource"/>
/// handles to the concrete imported textures.
/// </summary>
public sealed class D3D12RenderPassContext
{
    private readonly D3D12FrameGraph _graph;

    internal D3D12RenderPassContext(D3D12FrameGraph graph, D3D12CommandList commandList)
    {
        _graph = graph;
        CommandList = commandList;
    }

    public D3D12CommandList CommandList { get; }

    public D3D12Texture Texture(FrameGraphResource handle) => _graph.ResolveTexture(handle);
}
