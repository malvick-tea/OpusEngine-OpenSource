using System;
using System.Collections.Generic;
using Opus.Engine.FrameGraph;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// D3D12-specific frame graph: orchestrates a sequence of <see cref="D3D12RenderPass"/>es,
/// resolves <see cref="FrameGraphResource"/> handles to imported textures, hands the
/// shared <see cref="D3D12CommandList"/> to each pass.
///
/// Per-concern implementation lives in single-responsibility partials in this folder:
/// <list type="bullet">
///   <item><description><c>D3D12FrameGraph.Resources.cs</c> — texture imports + final-state hints + pass registration.</description></item>
///   <item><description><c>D3D12FrameGraph.Compile.cs</c> — auto-barrier planning, usage→state mapping, supporting record types.</description></item>
///   <item><description><c>D3D12FrameGraph.Execute.cs</c> — runs the planned barrier batches + each pass against the command list.</description></item>
/// </list>
///
/// R-5.a surface (this milestone):
/// <list type="bullet">
/// <item><description>Imported textures only — caller registers external resources
///     (swap-chain back buffer, depth target) via <see cref="ImportTexture"/>.</description></item>
/// <item><description>Passes still record barriers + state manually; the graph just
///     orders them.</description></item>
/// <item><description>No transient allocation, no aliasing, no automatic barrier
///     insertion.</description></item>
/// </list>
/// R-5.b adds automatic barrier insertion based on the per-pass resource-usage declarations
/// recorded through <see cref="D3D12FrameGraphBuilder"/>. R-5.c adds transient pool + aliasing.
/// </summary>
public sealed unsafe partial class D3D12FrameGraph : IDisposable
{
    private readonly List<D3D12RenderPass> _passes = new();
    private readonly List<D3D12Texture> _imports = new();
    private readonly List<D3D12Texture> _wrappersToDispose = new();
    private readonly Dictionary<int, ResourceStates> _importInitialStates = new();
    private readonly Dictionary<int, ResourceStates> _finalStates = new();
    private readonly Dictionary<int, List<ResourceUsageDeclaration>> _passUsages = new();
    private readonly Dictionary<int, List<PlannedBarrier>> _passBarriers = new();
    private readonly List<PlannedBarrier> _finalBarriers = new();
    private int _nextHandleId;
    private bool _compiled;
    private bool _disposed;

    public IReadOnlyList<D3D12RenderPass> Passes => _passes;

    public bool IsCompiled => _compiled;

    /// <summary>Clears the per-frame state (pass list, imports, declared usages, barrier
    /// plan, final-state map). Call once per frame before re-declaring the graph.
    /// Equivalent of <c>IFrameGraph.BeginFrame</c> in the abstract contract.</summary>
    public void Reset()
    {
        _passes.Clear();
        _imports.Clear();
        _importInitialStates.Clear();
        _finalStates.Clear();
        _passUsages.Clear();
        _passBarriers.Clear();
        _finalBarriers.Clear();
        _nextHandleId = 0;
        _compiled = false;

        foreach (var wrapper in _wrappersToDispose)
        {
            wrapper.Dispose();
        }

        _wrappersToDispose.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Reset();
        _disposed = true;
    }

    internal D3D12Texture ResolveTexture(FrameGraphResource handle)
    {
        if (handle.Kind != FrameGraphResourceKind.Texture)
        {
            throw new InvalidOperationException($"Handle {handle.Id} is not a texture (kind {handle.Kind}).");
        }

        if (handle.Id < 0 || handle.Id >= _imports.Count)
        {
            throw new InvalidOperationException($"Handle {handle.Id} out of range (imports: {_imports.Count}).");
        }

        return _imports[handle.Id];
    }

    private static void EnsureSlot<T>(List<T> list, int index)
        where T : class
    {
        while (list.Count <= index)
        {
            list.Add(null!);
        }
    }
}
