using System;

namespace Opus.Engine.Audio;

/// <summary>Starts and owns streaming sound-effect loops such as engines, tracks, and
/// turret motors. Callers retain the returned handle for live volume changes and stop it
/// when the gameplay state no longer needs the loop.</summary>
public interface ILoopingSfxPlayer
{
    ILoopingSfxHandle PlayLoop(string vfsPath, float volumeMultiplier = 1f);
}

/// <summary>One live looping SFX channel.</summary>
public interface ILoopingSfxHandle : IDisposable
{
    bool IsPlaying { get; }

    void SetVolume(float volumeMultiplier);

    void Stop();
}
