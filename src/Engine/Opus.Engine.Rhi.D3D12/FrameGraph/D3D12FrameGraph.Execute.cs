using System;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Frame-graph execution: emits the barrier batches that <c>Compile</c> planned
/// and runs each pass against the supplied command list. Compile must have run for the
/// current declaration set; calling Execute before Compile throws.</summary>
public sealed unsafe partial class D3D12FrameGraph
{
    /// <summary>Executes each pass against <paramref name="commandList"/> in declaration
    /// order, emitting planned barrier transitions immediately before each pass. The
    /// caller has already opened the command list (Begin) and will close it + submit
    /// after this returns.</summary>
    public void Execute(D3D12CommandList commandList)
    {
        if (!_compiled)
        {
            throw new InvalidOperationException("Execute called before Compile.");
        }

        if (commandList == null)
        {
            throw new ArgumentNullException(nameof(commandList));
        }

        var context = new D3D12RenderPassContext(this, commandList);

        for (var i = 0; i < _passes.Count; i++)
        {
            EmitPlannedBarriers(commandList, i);
            _passes[i].Execute(context);
        }

        foreach (var barrier in _finalBarriers)
        {
            var texture = _imports[barrier.ResourceId];
            commandList.ResourceBarrierTransition(texture.Native, barrier.Before, barrier.After);
        }
    }

    private void EmitPlannedBarriers(D3D12CommandList commandList, int passIndex)
    {
        if (!_passBarriers.TryGetValue(passIndex, out var barriers))
        {
            return;
        }

        foreach (var barrier in barriers)
        {
            var texture = _imports[barrier.ResourceId];
            commandList.ResourceBarrierTransition(texture.Native, barrier.Before, barrier.After);
        }
    }
}
