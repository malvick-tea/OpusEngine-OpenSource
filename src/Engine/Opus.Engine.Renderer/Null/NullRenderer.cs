using Opus.Engine.FrameGraph;
using Opus.Engine.FrameGraph.Null;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Null;

namespace Opus.Engine.Renderer.Null;

/// <summary>
/// Headless renderer. Wires a <see cref="NullRhiDevice"/> + <see cref="NullFrameGraph"/>
/// and accepts BeginFrame / EndFrame calls — no GPU work happens, no display output.
///
/// Lets the entire engine stack boot in places where a live GPU would be undesirable:
/// <list type="bullet">
/// <item><description>Unit tests for ECS extract logic + pass declarations.</description></item>
/// <item><description>Server-side simulation (Phase H multiplayer).</description></item>
/// <item><description>Asset-bake CLI tooling.</description></item>
/// <item><description>CI smoke runs without an X server / Windows desktop.</description></item>
/// </list>
///
/// Frame counter advances on each BeginFrame so temporal-effect logic still sees a
/// monotonically-increasing value.
/// </summary>
public sealed class NullRenderer : IRenderer
{
    private ulong _frameIndex;

    public NullRenderer()
    {
        Device = new NullRhiDevice();
        FrameGraph = new NullFrameGraph();
    }

    public IRhiDevice Device { get; }

    public IFrameGraph FrameGraph { get; }

    public IFrameContext BeginFrame(FrameCameraSet cameras, LightingSetup lighting, PostFxSetup postFx)
    {
        _frameIndex++;
        FrameGraph.BeginFrame();
        return new NullFrameContext(cameras, lighting, postFx, _frameIndex);
    }

    public void EndFrame(IFrameContext context)
    {
        FrameGraph.Compile();
        FrameGraph.Execute();
        FrameGraph.EndFrame();
    }

    public void Dispose()
    {
        FrameGraph.Dispose();
        Device.Dispose();
    }
}

internal sealed class NullFrameContext : IFrameContext
{
    public NullFrameContext(FrameCameraSet cameras, LightingSetup lighting, PostFxSetup postFx, ulong frameIndex)
    {
        Cameras = cameras;
        Lighting = lighting;
        PostFx = postFx;
        FrameIndex = frameIndex;
    }

    public FrameCameraSet Cameras { get; }

    public LightingSetup Lighting { get; }

    public PostFxSetup PostFx { get; }

    public ulong FrameIndex { get; }
}
