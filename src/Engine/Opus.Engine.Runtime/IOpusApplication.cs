using Opus.Foundation;

namespace Opus.Engine.Runtime;

/// <summary>
/// Runtime-facing application callbacks driven by <see cref="OpusHost"/>.
/// Game-side code implements this boundary; engine tests and tools can use
/// <see cref="OpusNoopApplication"/> when no game behavior is needed.
/// </summary>
public interface IOpusApplication
{
    void OnStarted(OpusHostContext context);

    void FixedTick(GameTime time);

    void Render(OpusRenderFrame frame);

    void OnPaused(OpusHostContext context);

    void OnResumed(OpusHostContext context);

    void OnStopping(OpusHostContext context);
}
