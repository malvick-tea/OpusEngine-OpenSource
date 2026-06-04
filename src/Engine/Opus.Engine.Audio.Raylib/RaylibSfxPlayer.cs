using System;
using System.Collections.Generic;
using Raylib_cs;

namespace Opus.Engine.Audio.Raylib;

/// <summary>Cached fire-and-forget Raylib sound effects.</summary>
public sealed class RaylibSfxPlayer : ISfxPlayer, IDisposable
{
    private readonly RaylibAudioDevice _device;
    private readonly Func<string, string> _realize;
    private readonly Dictionary<string, Sound> _sounds = new(StringComparer.Ordinal);
    private bool _disposed;

    public RaylibSfxPlayer(RaylibAudioDevice device, Func<string, string> realize)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _realize = realize ?? throw new ArgumentNullException(nameof(realize));
    }

    public void Play(string vfsPath, float volumeMultiplier = 1f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_device.IsReady)
        {
            return;
        }

        try
        {
            if (!_sounds.TryGetValue(vfsPath, out var sound))
            {
                sound = global::Raylib_cs.Raylib.LoadSound(_realize(vfsPath));
                _sounds.Add(vfsPath, sound);
            }

            global::Raylib_cs.Raylib.SetSoundVolume(sound, Gain.Sanitize(_device.Mixer.EffectiveSfx * volumeMultiplier));
            global::Raylib_cs.Raylib.PlaySound(sound);
        }
        catch (Exception)
        {
            // Optional audio must never take down the client when one asset or output
            // device is unavailable.
            return;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_device.IsReady)
        {
            foreach (var sound in _sounds.Values)
            {
                global::Raylib_cs.Raylib.UnloadSound(sound);
            }
        }

        _sounds.Clear();
    }
}
