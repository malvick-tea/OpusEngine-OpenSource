using System.Collections.Generic;
using Opus.Engine.FrameGraph;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Compile-time barrier planning for the frame graph. Walks every declared pass
/// in order, coalesces multi-declaration usages into a single target D3D12 state, and
/// records the transition batches that <c>Execute</c> emits ahead of each pass.</summary>
public sealed unsafe partial class D3D12FrameGraph
{
    /// <summary>
    /// Plans resource-state transitions for every pass:
    /// <list type="number">
    /// <item><description>Validates that every declared handle has been imported.</description></item>
    /// <item><description>Walks passes in declaration order, mapping each declared
    ///     <see cref="FrameGraphResourceUsage"/> to a D3D12 <see cref="ResourceStates"/>.
    ///     Inserts a transition barrier in <c>_passBarriers[i]</c> whenever the resource's
    ///     current state differs from the pass's required state.</description></item>
    /// <item><description>Records a final-transition barrier batch for any resource the
    ///     caller registered via <see cref="EnsureFinalState"/>.</description></item>
    /// </list>
    /// Same-frame Compile/Execute pair is idempotent — calling Compile twice produces the
    /// same barrier plan.
    /// </summary>
    public void Compile()
    {
        _passBarriers.Clear();
        _finalBarriers.Clear();

        var currentStates = new Dictionary<int, ResourceStates>();
        foreach (var (id, state) in _importInitialStates)
        {
            currentStates[id] = state;
        }

        for (var passIdx = 0; passIdx < _passes.Count; passIdx++)
        {
            if (!_passUsages.TryGetValue(passIdx, out var usages))
            {
                continue;
            }

            PlanPassBarriers(passIdx, usages, currentStates);
        }

        foreach (var (resId, finalState) in _finalStates)
        {
            if (!currentStates.TryGetValue(resId, out var beforeState))
            {
                beforeState = ResourceStates.Common;
            }

            if (beforeState != finalState)
            {
                _finalBarriers.Add(new PlannedBarrier(resId, beforeState, finalState));
            }
        }

        _compiled = true;
    }

    private void PlanPassBarriers(
        int passIdx,
        List<ResourceUsageDeclaration> usages,
        Dictionary<int, ResourceStates> currentStates)
    {
        // Coalesce multiple declarations on the same resource into a single target
        // state (e.g. a pass that reads AND writes the same texture).
        var perResource = new Dictionary<int, FrameGraphResourceUsage>();
        foreach (var declaration in usages)
        {
            if (declaration.Handle.Kind != FrameGraphResourceKind.Texture)
            {
                continue;
            }

            if (declaration.Handle.Id < 0 || declaration.Handle.Id >= _imports.Count || _imports[declaration.Handle.Id] == null)
            {
                throw new System.InvalidOperationException(
                    $"Pass '{_passes[passIdx].Name}' declared usage of handle {declaration.Handle.Id} but no import exists for that slot.");
            }

            perResource.TryGetValue(declaration.Handle.Id, out var existing);
            perResource[declaration.Handle.Id] = existing | declaration.Usage;
        }

        var passBarriers = new List<PlannedBarrier>();
        foreach (var (resId, usage) in perResource)
        {
            var targetState = UsageToState(usage);
            if (!currentStates.TryGetValue(resId, out var beforeState))
            {
                beforeState = ResourceStates.Common;
            }

            if (beforeState != targetState)
            {
                passBarriers.Add(new PlannedBarrier(resId, beforeState, targetState));
                currentStates[resId] = targetState;
            }
        }

        if (passBarriers.Count > 0)
        {
            _passBarriers[passIdx] = passBarriers;
        }
    }

    private static ResourceStates UsageToState(FrameGraphResourceUsage usage)
    {
        var state = ResourceStates.Common;

        if ((usage & FrameGraphResourceUsage.ColorTarget) != 0)
        {
            state |= ResourceStates.RenderTarget;
        }

        if ((usage & FrameGraphResourceUsage.DepthTarget) != 0)
        {
            state |= ResourceStates.DepthWrite;
        }

        if ((usage & FrameGraphResourceUsage.Read) != 0)
        {
            state |= ResourceStates.PixelShaderResource;
        }

        if ((usage & FrameGraphResourceUsage.UnorderedAccess) != 0)
        {
            state |= ResourceStates.UnorderedAccess;
        }

        if ((usage & FrameGraphResourceUsage.IndirectArgs) != 0)
        {
            state |= ResourceStates.IndirectArgument;
        }

        return state;
    }
}

/// <summary>How a pass interacts with one resource. Recorded by the builder during
/// <see cref="D3D12RenderPass.Setup"/>; consumed by the planning pass to insert
/// barriers.</summary>
internal readonly record struct ResourceUsageDeclaration(
    FrameGraphResource Handle,
    FrameGraphResourceUsage Usage);

/// <summary>A planned state transition emitted before a specific pass executes. Built
/// during <see cref="D3D12FrameGraph.Compile"/>, consumed during execution.</summary>
internal readonly record struct PlannedBarrier(
    int ResourceId,
    ResourceStates Before,
    ResourceStates After);
