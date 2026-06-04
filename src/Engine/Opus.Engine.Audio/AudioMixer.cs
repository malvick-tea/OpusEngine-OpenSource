using System;

namespace Opus.Engine.Audio;

/// <summary>
/// Three linear-gain knobs (master / music / sfx) that the host's settings screen
/// writes to and the concrete audio backend reads from. Gains compose multiplicatively
/// — <see cref="EffectiveMusic"/> = <c>Master × Music</c>, <see cref="EffectiveSfx"/> =
/// <c>Master × Sfx</c>. Subscribers (the Raylib backend, post-fx busses, the menu
/// audio preview) listen on <see cref="Changed"/> to re-apply gain to live voices.
/// </summary>
/// <remarks>
/// <para>
/// Backend-agnostic by design — this lives in <c>Opus.Engine.Audio</c> with zero
/// dependency on Raylib / Wwise / FMOD. The same mixer drives every audio backend.
/// </para>
/// <para>
/// Gains are clamped to <c>[0, 1]</c>. NaN / Infinity / negative inputs round to zero;
/// values above 1 round down. There is no "boost above unity" knob — boosting is the
/// content side's job (re-master the source). This is per ADR-0021 (Wwise eventually
/// replaces this; the mixer surface is identical so screens don't have to change).
/// </para>
/// </remarks>
public sealed class AudioMixer
{
    private const float MinGain = 0f;
    private const float MaxGain = 1f;

    private float _master;
    private float _music;
    private float _sfx;

    public AudioMixer(float masterGain, float musicGain, float sfxGain)
    {
        _master = Sanitise(masterGain);
        _music = Sanitise(musicGain);
        _sfx = Sanitise(sfxGain);
    }

    public event Action? Changed;

    public float MasterGain => _master;

    public float MusicGain => _music;

    public float SfxGain => _sfx;

    public float EffectiveMusic => _master * _music;

    public float EffectiveSfx => _master * _sfx;

    public void SetMaster(float gain) => Mutate(ref _master, gain);

    public void SetMusic(float gain) => Mutate(ref _music, gain);

    public void SetSfx(float gain) => Mutate(ref _sfx, gain);

    /// <summary>Applies all three gains in one notification so subscribers don't see an
    /// inconsistent partial state when the settings screen flushes a batch update.</summary>
    public void Set(float master, float music, float sfx)
    {
        var newMaster = Sanitise(master);
        var newMusic = Sanitise(music);
        var newSfx = Sanitise(sfx);
        if (newMaster == _master && newMusic == _music && newSfx == _sfx)
        {
            return;
        }

        _master = newMaster;
        _music = newMusic;
        _sfx = newSfx;
        Changed?.Invoke();
    }

    private void Mutate(ref float field, float value)
    {
        var sanitised = Sanitise(value);
        if (sanitised == field)
        {
            return;
        }

        field = sanitised;
        Changed?.Invoke();
    }

    private static float Sanitise(float value)
    {
        if (!float.IsFinite(value) || value < MinGain)
        {
            return MinGain;
        }

        return value > MaxGain ? MaxGain : value;
    }
}
