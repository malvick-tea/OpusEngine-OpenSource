using FluentAssertions;
using Opus.Engine.Audio;
using Xunit;

namespace Opus.Engine.Audio.Tests;

/// <summary>Locks the gain arithmetic + change-notification semantics of
/// <see cref="AudioMixer"/>. The mixer is the single source of truth for every
/// volume slider in the game; broken arithmetic here = broken audio everywhere.</summary>
public sealed class AudioMixerTests
{
    [Fact]
    public void Constructor_clamps_inputs_into_unit_range()
    {
        var mixer = new AudioMixer(masterGain: 2.5f, musicGain: -0.3f, sfxGain: 0.5f);
        mixer.MasterGain.Should().Be(1f);
        mixer.MusicGain.Should().Be(0f);
        mixer.SfxGain.Should().Be(0.5f);
    }

    [Fact]
    public void Effective_gains_compose_multiplicatively()
    {
        var mixer = new AudioMixer(0.8f, 0.5f, 0.9f);
        mixer.EffectiveMusic.Should().BeApproximately(0.4f, 1e-6f);
        mixer.EffectiveSfx.Should().BeApproximately(0.72f, 1e-6f);
    }

    [Fact]
    public void Set_individual_gain_clamps_and_fires_changed_once()
    {
        var mixer = new AudioMixer(0.5f, 0.5f, 0.5f);
        var fired = 0;
        mixer.Changed += () => fired++;

        mixer.SetMaster(1.5f);

        mixer.MasterGain.Should().Be(1f);
        fired.Should().Be(1);
    }

    [Fact]
    public void Set_individual_gain_does_not_fire_when_value_unchanged()
    {
        var mixer = new AudioMixer(0.5f, 0.5f, 0.5f);
        var fired = 0;
        mixer.Changed += () => fired++;

        mixer.SetMaster(0.5f);
        mixer.SetMusic(0.5f);
        mixer.SetSfx(0.5f);

        fired.Should().Be(0);
    }

    [Fact]
    public void Set_batch_fires_changed_once_per_coherent_update()
    {
        var mixer = new AudioMixer(0.5f, 0.5f, 0.5f);
        var fired = 0;
        mixer.Changed += () => fired++;

        mixer.Set(0.8f, 0.6f, 0.9f);

        fired.Should().Be(1);
        mixer.MasterGain.Should().Be(0.8f);
        mixer.MusicGain.Should().Be(0.6f);
        mixer.SfxGain.Should().Be(0.9f);
    }

    [Fact]
    public void Set_batch_does_not_fire_when_no_value_changed()
    {
        var mixer = new AudioMixer(0.5f, 0.5f, 0.5f);
        var fired = 0;
        mixer.Changed += () => fired++;

        mixer.Set(0.5f, 0.5f, 0.5f);

        fired.Should().Be(0);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void Non_finite_or_negative_gains_clamp_to_zero(float bad)
    {
        var mixer = new AudioMixer(1f, 1f, 1f);
        mixer.SetMaster(bad);
        mixer.MasterGain.Should().Be(0f);
    }

    [Fact]
    public void Above_unity_gain_clamps_to_one()
    {
        var mixer = new AudioMixer(1f, 1f, 1f);
        mixer.SetMaster(1.0001f);
        mixer.MasterGain.Should().Be(1f);
    }

    [Fact]
    public void Effective_gain_returns_zero_when_either_factor_is_zero()
    {
        var mixer = new AudioMixer(masterGain: 0f, musicGain: 1f, sfxGain: 1f);
        mixer.EffectiveMusic.Should().Be(0f);
        mixer.EffectiveSfx.Should().Be(0f);
    }
}
