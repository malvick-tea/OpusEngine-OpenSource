namespace Opus.Engine.Audio;

/// <summary>Headless loop player. Returns inert handles so gameplay can manage channel
/// lifetime without branching on the selected backend.</summary>
public sealed class NullLoopingSfxPlayer : ILoopingSfxPlayer
{
    public ILoopingSfxHandle PlayLoop(string vfsPath, float volumeMultiplier = 1f) =>
        new NullLoopingSfxHandle();
}

/// <summary>Inert looping SFX channel for headless hosts and tests.</summary>
public sealed class NullLoopingSfxHandle : ILoopingSfxHandle
{
    public bool IsPlaying { get; private set; } = true;

    public void SetVolume(float volumeMultiplier)
    {
    }

    public void Stop() => IsPlaying = false;

    public void Dispose() => Stop();
}
