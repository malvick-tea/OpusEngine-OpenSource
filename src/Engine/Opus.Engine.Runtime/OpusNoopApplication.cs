using Opus.Foundation;

namespace Opus.Engine.Runtime;

/// <summary>No-op application used by headless smoke runs and tooling hosts.</summary>
public sealed class OpusNoopApplication : IOpusApplication
{
    public void OnStarted(OpusHostContext context)
    {
    }

    public void FixedTick(GameTime time)
    {
    }

    public void Render(OpusRenderFrame frame)
    {
    }

    public void OnPaused(OpusHostContext context)
    {
    }

    public void OnResumed(OpusHostContext context)
    {
    }

    public void OnStopping(OpusHostContext context)
    {
    }
}
