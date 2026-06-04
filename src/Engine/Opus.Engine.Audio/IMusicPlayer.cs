namespace Opus.Engine.Audio;

/// <summary>
/// Streaming music playback — exactly one track active at a time. <see cref="Play"/>
/// switches tracks (the old one stops cleanly); <see cref="Stop"/> halts playback.
/// Effective gain comes from <see cref="AudioMixer.EffectiveMusic"/>; the backend
/// re-applies gain on every mixer change so the volume slider takes effect live.
/// </summary>
/// <remarks>
/// The backend resolves <paramref name="vfsPath"/> via the host's <c>IVfs</c> and
/// streams from disk (music files are typically MB-sized; loading them into memory
/// like SFX would waste RAM). Loop semantics: when <c>loop = true</c>, the stream
/// seamlessly restarts at EOF; when <c>loop = false</c>, the track plays once and
/// <see cref="IsPlaying"/> flips to false on completion.
/// </remarks>
public interface IMusicPlayer
{
    /// <summary>Starts streaming <paramref name="vfsPath"/>. Any prior track stops.</summary>
    void Play(string vfsPath, bool loop = true);

    /// <summary>Stops the current track. No-op if nothing is playing.</summary>
    void Stop();

    /// <summary>True while a track is actively streaming.</summary>
    bool IsPlaying { get; }
}
