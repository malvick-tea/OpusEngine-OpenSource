using System;
using System.Collections.Generic;
using Raylib_cs;

namespace Opus.Engine.Audio.Raylib;

/// <summary>Raylib/miniaudio output device. Failure to open output is non-fatal: hosts
/// remain playable in headless environments and all players degrade to no-ops.</summary>
public sealed class RaylibAudioDevice : IAudioDevice
{
    private readonly List<RaylibLoopingSfxHandle> _loops = new();
    private bool _disposed;

    public RaylibAudioDevice(AudioMixer mixer)
    {
        Mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
        try
        {
            global::Raylib_cs.Raylib.InitAudioDevice();
            IsReady = global::Raylib_cs.Raylib.IsAudioDeviceReady();
        }
        catch (Exception)
        {
            IsReady = false;
        }
    }

    public AudioMixer Mixer { get; }

    public bool IsReady { get; private set; }

    public void Update()
    {
        if (_disposed || !IsReady)
        {
            return;
        }

        foreach (var loop in _loops.ToArray())
        {
            loop.Pump();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var loop in _loops.ToArray())
        {
            loop.Dispose();
        }

        if (IsReady)
        {
            global::Raylib_cs.Raylib.CloseAudioDevice();
            IsReady = false;
        }
    }

    internal void Register(RaylibLoopingSfxHandle loop)
    {
        if (!_disposed && IsReady)
        {
            _loops.Add(loop);
        }
    }

    internal void Unregister(RaylibLoopingSfxHandle loop) => _loops.Remove(loop);
}
