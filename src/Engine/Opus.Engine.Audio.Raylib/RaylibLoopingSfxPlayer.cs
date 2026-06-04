using System;
using Raylib_cs;

namespace Opus.Engine.Audio.Raylib;

/// <summary>Streaming Raylib loop player for long-running SFX channels.</summary>
public sealed class RaylibLoopingSfxPlayer : ILoopingSfxPlayer
{
    private readonly RaylibAudioDevice _device;
    private readonly Func<string, string> _realize;

    public RaylibLoopingSfxPlayer(RaylibAudioDevice device, Func<string, string> realize)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _realize = realize ?? throw new ArgumentNullException(nameof(realize));
    }

    public ILoopingSfxHandle PlayLoop(string vfsPath, float volumeMultiplier = 1f) =>
        new RaylibLoopingSfxHandle(_device, _realize, vfsPath, volumeMultiplier);
}

internal sealed class RaylibLoopingSfxHandle : ILoopingSfxHandle
{
    private readonly RaylibAudioDevice _device;
    private Music _music;
    private float _volumeMultiplier;
    private bool _loaded;
    private bool _disposed;

    public RaylibLoopingSfxHandle(
        RaylibAudioDevice device,
        Func<string, string> realize,
        string vfsPath,
        float volumeMultiplier)
    {
        _device = device;
        _volumeMultiplier = Gain.Sanitize(volumeMultiplier);
        if (!device.IsReady)
        {
            return;
        }

        try
        {
            _music = global::Raylib_cs.Raylib.LoadMusicStream(realize(vfsPath));
            _music.Looping = true;
            _loaded = true;
            device.Mixer.Changed += ApplyVolume;
            device.Register(this);
            ApplyVolume();
            global::Raylib_cs.Raylib.PlayMusicStream(_music);
        }
        catch (Exception)
        {
            Dispose();
        }
    }

    public bool IsPlaying =>
        !_disposed &&
        _loaded &&
        global::Raylib_cs.Raylib.IsMusicStreamPlaying(_music);

    public void SetVolume(float volumeMultiplier)
    {
        _volumeMultiplier = Gain.Sanitize(volumeMultiplier);
        ApplyVolume();
    }

    public void Stop()
    {
        if (!_disposed && _loaded)
        {
            global::Raylib_cs.Raylib.StopMusicStream(_music);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _device.Mixer.Changed -= ApplyVolume;
        _device.Unregister(this);
        if (_loaded && _device.IsReady)
        {
            global::Raylib_cs.Raylib.StopMusicStream(_music);
            global::Raylib_cs.Raylib.UnloadMusicStream(_music);
        }

        _loaded = false;
    }

    internal void Pump()
    {
        if (!_disposed && _loaded)
        {
            global::Raylib_cs.Raylib.UpdateMusicStream(_music);
        }
    }

    private void ApplyVolume()
    {
        if (!_disposed && _loaded)
        {
            global::Raylib_cs.Raylib.SetMusicVolume(
                _music,
                Gain.Sanitize(_device.Mixer.EffectiveSfx * _volumeMultiplier));
        }
    }
}

internal static class Gain
{
    public static float Sanitize(float value)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            return 0f;
        }

        return Math.Min(value, 1f);
    }
}
