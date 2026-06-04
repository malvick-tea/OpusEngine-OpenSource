using System.Collections.Generic;

namespace Opus.Engine.FrameGraph.Null;

/// <summary>
/// Headless frame graph. Records pass declarations, runs <see cref="IRenderPass.Setup"/>
/// against a null builder, optionally runs <see cref="IRenderPass.Execute"/> against a
/// null context — but emits no GPU work. Used by tests, asset bake, and headless boot.
///
/// Useful even without a live GPU: lets us unit-test pass graphs (e.g. "does the
/// ShadowPass declare its read on the depth pre-pass output?") without standing up a
/// device.
/// </summary>
public sealed class NullFrameGraph : IFrameGraph
{
    private readonly List<IRenderPass> _passes = new();
    private bool _compiled;
    private bool _inFrame;

    public IReadOnlyList<IRenderPass> Passes => _passes;

    public bool IsInFrame => _inFrame;

    public bool IsCompiled => _compiled;

    public void BeginFrame()
    {
        _passes.Clear();
        _compiled = false;
        _inFrame = true;
    }

    public void AddPass(IRenderPass pass)
    {
        if (!_inFrame)
        {
            throw new System.InvalidOperationException("AddPass called outside BeginFrame/EndFrame bracket.");
        }

        _passes.Add(pass);
        _compiled = false;
    }

    public void Compile()
    {
        var builder = new NullFrameGraphBuilder();
        foreach (var pass in _passes)
        {
            pass.Setup(builder);
        }

        _compiled = true;
    }

    public void Execute()
    {
        if (!_compiled)
        {
            throw new System.InvalidOperationException("Execute called before Compile.");
        }

        // Null backend: pass.Execute is a no-op against a null context. We still call it
        // so test assertions that "the pass ran" hold.
        var context = new NullRenderPassContext();
        foreach (var pass in _passes)
        {
            pass.Execute(context);
        }
    }

    public void EndFrame()
    {
        _inFrame = false;
    }

    public void Dispose()
    {
        _passes.Clear();
    }
}

internal sealed class NullFrameGraphBuilder : IFrameGraphBuilder
{
    private int _nextHandleId;

    public FrameGraphResource CreateTransientTexture(FrameGraphTextureDescription description) =>
        new(++_nextHandleId, FrameGraphResourceKind.Texture);

    public FrameGraphResource CreateTransientBuffer(FrameGraphBufferDescription description) =>
        new(++_nextHandleId, FrameGraphResourceKind.Buffer);

    public void Read(FrameGraphResource handle)
    {
    }

    public void Write(FrameGraphResource handle)
    {
    }

    public void ColorTarget(FrameGraphResource handle)
    {
    }

    public void DepthTarget(FrameGraphResource handle)
    {
    }
}

internal sealed class NullRenderPassContext : IRenderPassContext
{
    public Rhi.IRhiCommandList CommandList { get; } = new Rhi.Null.NullCommandList("frame-graph-null");

    public Rhi.IRhiTexture ResolveTexture(FrameGraphResource handle) =>
        throw new System.InvalidOperationException(
            "Null frame graph does not resolve to real textures. " +
            "Tests should assert on the declaration, not on the resolved resource.");

    public Rhi.IRhiBuffer ResolveBuffer(FrameGraphResource handle) =>
        throw new System.InvalidOperationException(
            "Null frame graph does not resolve to real buffers.");
}
