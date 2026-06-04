using Opus.Engine.FrameGraph;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Single D3D12-typed render pass — counterpart of <see cref="Opus.Engine.FrameGraph.IRenderPass"/>
/// that drops the abstract <see cref="Rhi.IRhiCommandList"/> in favour of the concrete
/// <see cref="D3D12CommandList"/>, so passes can call the full recording surface without
/// downcasting on every line.
///
/// Two-phase lifecycle:
/// <list type="number">
/// <item><description><see cref="Setup"/> — declare resource reads / writes / RT bindings
///     through the builder. Pure, called once per <c>D3D12FrameGraph.AddPass</c>.</description></item>
/// <item><description><see cref="Execute"/> — record GPU commands against the shared command
///     list. Called once per <c>D3D12FrameGraph.Execute</c>.</description></item>
/// </list>
/// </summary>
public abstract class D3D12RenderPass
{
    public abstract string Name { get; }

    public abstract void Setup(D3D12FrameGraphBuilder builder);

    public abstract void Execute(D3D12RenderPassContext context);
}
