using Opus.Engine.Renderer;

namespace Opus.Engine.Renderer.Direct3D12;

/// <summary>Per-frame state for the D3D12 renderer — carries the camera / lighting /
/// post-FX captures from <c>BeginFrame</c> through to passes that read them via
/// <see cref="IFrameContext"/>. Frame index is a monotonic counter that the renderer
/// increments each time <see cref="D3D12Renderer.BeginFrame"/> opens a new frame.</summary>
internal sealed class D3D12FrameContext : IFrameContext
{
    public D3D12FrameContext(
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx,
        ulong frameIndex)
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
