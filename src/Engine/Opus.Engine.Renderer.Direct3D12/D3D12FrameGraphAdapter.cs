using System;
using System.Collections.Generic;
using Opus.Engine.FrameGraph;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12;

/// <summary>
/// <see cref="IFrameGraph"/> facade over the concrete <see cref="D3D12FrameGraph"/>.
/// Bridges two surfaces with deliberately different shapes:
/// <list type="bullet">
/// <item><description><see cref="IFrameGraph.AddPass(IRenderPass)"/> takes an abstract pass — the D3D12 graph
///     requires a <c>D3D12RenderPass</c> with the typed builder + context. Calling this
///     through the abstract API throws; pass authors register concrete passes via
///     <see cref="D3D12Renderer.FrameGraphConcrete"/> until R-21+ unifies the surfaces.</description></item>
/// <item><description><see cref="IFrameGraph.Execute()"/> takes no arguments — the D3D12 graph needs a
///     command list. The adapter holds a reference handed in by the renderer at
///     construction; <see cref="D3D12Renderer.BeginFrame"/> opens the list, the
///     adapter's <see cref="Execute"/> feeds it to the concrete graph, the renderer
///     closes + submits + presents in <c>EndFrame</c>.</description></item>
/// </list>
/// </summary>
internal sealed class D3D12FrameGraphAdapter : IFrameGraph
{
    private readonly D3D12FrameGraph _concrete;
    private readonly D3D12CommandList _commandList;
    private bool _inFrame;

    public D3D12FrameGraphAdapter(D3D12FrameGraph concrete, D3D12CommandList commandList)
    {
        _concrete = concrete ?? throw new ArgumentNullException(nameof(concrete));
        _commandList = commandList ?? throw new ArgumentNullException(nameof(commandList));
    }

    /// <summary>The pass list returned here is empty even when the concrete graph is
    /// holding D3D12 passes — they implement <see cref="D3D12RenderPass"/>, not
    /// <see cref="IRenderPass"/>, so they can't appear in this abstract collection.
    /// Use <see cref="D3D12FrameGraph.Passes"/> for the typed view.</summary>
    public IReadOnlyList<IRenderPass> Passes => Array.Empty<IRenderPass>();

    public void BeginFrame()
    {
        if (_inFrame)
        {
            throw new InvalidOperationException("D3D12FrameGraphAdapter.BeginFrame called twice without matching EndFrame.");
        }

        _concrete.Reset();
        _inFrame = true;
    }

    public void AddPass(IRenderPass pass) =>
        throw new NotSupportedException(
            "D3D12FrameGraph requires passes derived from D3D12RenderPass (typed builder + command-list context). "
            + "Add concrete passes via D3D12Renderer.FrameGraphConcrete.AddPass(D3D12RenderPass).");

    public void Compile() => _concrete.Compile();

    public void Execute() => _concrete.Execute(_commandList);

    public void EndFrame()
    {
        _inFrame = false;
    }

    public void Dispose()
    {
        _concrete.Dispose();
    }
}
