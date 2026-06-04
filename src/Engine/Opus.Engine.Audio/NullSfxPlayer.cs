using System.Collections.Generic;

namespace Opus.Engine.Audio;

/// <summary>
/// Headless sfx player. Records every Play call so tests can assert "the click sound
/// was triggered when the user pressed the button" without booting an audio backend.
/// </summary>
public sealed class NullSfxPlayer : ISfxPlayer
{
    private readonly List<(string Path, float Volume)> _plays = new();

    /// <summary>Read-only log of every Play call, in order.</summary>
    public IReadOnlyList<(string Path, float Volume)> Plays => _plays;

    public void Play(string vfsPath, float volumeMultiplier = 1f)
    {
        _plays.Add((vfsPath, volumeMultiplier));
    }

    /// <summary>Resets the recorded log between test cases.</summary>
    public void Reset() => _plays.Clear();
}
