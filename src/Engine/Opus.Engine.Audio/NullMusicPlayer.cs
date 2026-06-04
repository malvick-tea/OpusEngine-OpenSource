namespace Opus.Engine.Audio;

/// <summary>Headless music player. Tracks the current path / loop / IsPlaying state so
/// tests can verify "the menu music started on screen entry" without an audio backend.</summary>
public sealed class NullMusicPlayer : IMusicPlayer
{
    public string? CurrentPath { get; private set; }

    public bool CurrentLoop { get; private set; }

    public bool IsPlaying { get; private set; }

    public void Play(string vfsPath, bool loop = true)
    {
        CurrentPath = vfsPath;
        CurrentLoop = loop;
        IsPlaying = true;
    }

    public void Stop()
    {
        IsPlaying = false;
    }
}
