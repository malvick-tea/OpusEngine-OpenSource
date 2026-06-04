namespace Opus.Engine.Audio;

/// <summary>
/// Headless audio device — used by unit tests, server builds, and any host that wants
/// the audio API surface without an output device. Every method is a safe no-op; the
/// mixer still works (so settings-screen tests against a no-op device still verify the
/// notification chain).
/// </summary>
public sealed class NullAudioDevice : IAudioDevice
{
    public NullAudioDevice()
    {
        Mixer = new AudioMixer(masterGain: 1f, musicGain: 1f, sfxGain: 1f);
    }

    public AudioMixer Mixer { get; }

    public bool IsReady => false;

    public void Update()
    {
    }

    public void Dispose()
    {
    }
}
