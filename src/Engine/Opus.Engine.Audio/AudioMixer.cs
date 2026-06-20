using System;

namespace Opus.Engine.Audio;

/// <summary>
/// Thread-safe linear-gain controls shared by settings UI and audio backends.
/// Effective gains compose the master gain with the music or sound-effects gain.
/// </summary>
/// <remarks>
/// Gains are clamped to <c>[0, 1]</c>. Non-finite and negative inputs become zero.
/// Change callbacks run outside the state lock so subscribers may safely read the mixer.
/// </remarks>
public sealed class AudioMixer
{
    private const float MinGain = 0f;
    private const float MaxGain = 1f;

    private readonly object _sync = new();
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

    public float MasterGain
    {
        get
        {
            lock (_sync)
            {
                return _master;
            }
        }
    }

    public float MusicGain
    {
        get
        {
            lock (_sync)
            {
                return _music;
            }
        }
    }

    public float SfxGain
    {
        get
        {
            lock (_sync)
            {
                return _sfx;
            }
        }
    }

    public float EffectiveMusic
    {
        get
        {
            lock (_sync)
            {
                return _master * _music;
            }
        }
    }

    public float EffectiveSfx
    {
        get
        {
            lock (_sync)
            {
                return _master * _sfx;
            }
        }
    }

    public void SetMaster(float gain) => Mutate(ref _master, gain);

    public void SetMusic(float gain) => Mutate(ref _music, gain);

    public void SetSfx(float gain) => Mutate(ref _sfx, gain);

    /// <summary>Applies a coherent gain set and emits at most one notification.</summary>
    public void Set(float master, float music, float sfx)
    {
        var newMaster = Sanitise(master);
        var newMusic = Sanitise(music);
        var newSfx = Sanitise(sfx);
        Action? changed;
        lock (_sync)
        {
            if (newMaster == _master && newMusic == _music && newSfx == _sfx)
            {
                return;
            }

            _master = newMaster;
            _music = newMusic;
            _sfx = newSfx;
            changed = Changed;
        }

        changed?.Invoke();
    }

    private void Mutate(ref float field, float value)
    {
        var sanitised = Sanitise(value);
        Action? changed;
        lock (_sync)
        {
            if (sanitised == field)
            {
                return;
            }

            field = sanitised;
            changed = Changed;
        }

        changed?.Invoke();
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
