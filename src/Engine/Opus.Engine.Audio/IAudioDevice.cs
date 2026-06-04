using System;

namespace Opus.Engine.Audio;

/// <summary>
/// Backend audio device — holds the underlying audio output (Raylib's miniaudio /
/// Wwise's sound engine / FMOD studio system), owns the <see cref="AudioMixer"/>,
/// and gets pumped once per frame so the streaming layers (music decoding, voice
/// cleanup) can do their work.
/// </summary>
/// <remarks>
/// <para>
/// Concrete implementations: <c>Opus.Engine.Audio.Raylib.RaylibAudioDevice</c>
/// (alpha-grade), eventually <c>Opus.Engine.Audio.Wwise.WwiseAudioDevice</c> per
/// ADR-0021. Tests use <see cref="NullAudioDevice"/>.
/// </para>
/// <para>
/// A single device instance is registered as a DI singleton. <see cref="ISfxPlayer"/>
/// and <see cref="IMusicPlayer"/> reference the same device so they share the mixer
/// and the underlying voice pool.
/// </para>
/// </remarks>
public interface IAudioDevice : IDisposable
{
    /// <summary>Gain knobs (master / music / sfx). Writing to the mixer fires the
    /// device's apply-gain logic; the backend re-applies effective gain to every
    /// live voice / stream.</summary>
    AudioMixer Mixer { get; }

    /// <summary>True when the backend opened the audio output successfully. False on
    /// headless machines or when the device failed to initialise — players + music
    /// must no-op safely in that case.</summary>
    bool IsReady { get; }

    /// <summary>Per-frame pump. Drives music streaming + voice cleanup. Host calls
    /// once after input / before render.</summary>
    void Update();
}
