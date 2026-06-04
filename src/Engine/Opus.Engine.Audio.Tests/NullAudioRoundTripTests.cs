using FluentAssertions;
using Opus.Engine.Audio;
using Xunit;

namespace Opus.Engine.Audio.Tests;

/// <summary>The Null* implementations are first-class citizens — headless tests +
/// server builds + early boot before the audio device opens all consume them. Verify
/// they preserve the contract semantics (Update no-ops, IsReady is false, sfx Plays
/// recorded, music IsPlaying flips correctly).</summary>
public sealed class NullAudioRoundTripTests
{
    [Fact]
    public void NullAudioDevice_exposes_default_mixer_and_reports_not_ready()
    {
        using var device = new NullAudioDevice();
        device.IsReady.Should().BeFalse();
        device.Mixer.MasterGain.Should().Be(1f);
        device.Mixer.MusicGain.Should().Be(1f);
        device.Mixer.SfxGain.Should().Be(1f);
    }

    [Fact]
    public void NullAudioDevice_update_and_dispose_are_safe_idempotent()
    {
        var device = new NullAudioDevice();
        device.Update();
        device.Dispose();
        device.Dispose();
        device.Update();
    }

    [Fact]
    public void NullSfxPlayer_records_play_calls_in_order()
    {
        var sfx = new NullSfxPlayer();
        sfx.Play("res://audio/sfx/click.wav");
        sfx.Play("res://audio/sfx/fire.wav", 0.6f);

        sfx.Plays.Should().HaveCount(2);
        sfx.Plays[0].Path.Should().Be("res://audio/sfx/click.wav");
        sfx.Plays[0].Volume.Should().Be(1f);
        sfx.Plays[1].Path.Should().Be("res://audio/sfx/fire.wav");
        sfx.Plays[1].Volume.Should().Be(0.6f);
    }

    [Fact]
    public void NullSfxPlayer_reset_clears_recorded_plays()
    {
        var sfx = new NullSfxPlayer();
        sfx.Play("a");
        sfx.Reset();
        sfx.Plays.Should().BeEmpty();
    }

    [Fact]
    public void NullLoopingSfxPlayer_returns_handle_that_stops_idempotently()
    {
        var loops = new NullLoopingSfxPlayer();
        using var handle = loops.PlayLoop("res://audio/sfx/engine.ogg");

        handle.IsPlaying.Should().BeTrue();
        handle.SetVolume(0.4f);
        handle.Stop();
        handle.Stop();

        handle.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void NullMusicPlayer_play_marks_it_as_playing_with_path_and_loop_state()
    {
        var music = new NullMusicPlayer();
        music.Play("res://audio/music/menu_loop.ogg", loop: true);

        music.IsPlaying.Should().BeTrue();
        music.CurrentPath.Should().Be("res://audio/music/menu_loop.ogg");
        music.CurrentLoop.Should().BeTrue();
    }

    [Fact]
    public void NullMusicPlayer_play_then_stop_flips_isplaying_to_false()
    {
        var music = new NullMusicPlayer();
        music.Play("res://audio/music/track.ogg");
        music.Stop();

        music.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void NullMusicPlayer_play_replaces_previous_track()
    {
        var music = new NullMusicPlayer();
        music.Play("first.ogg", loop: true);
        music.Play("second.ogg", loop: false);

        music.CurrentPath.Should().Be("second.ogg");
        music.CurrentLoop.Should().BeFalse();
    }
}
