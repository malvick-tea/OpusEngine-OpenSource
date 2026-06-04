using System;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Scenes;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Scenes;

public sealed class AlphaSceneScaleProfileTests
{
    [Fact]
    public void Small_profile_matches_m5_smoke_shape()
    {
        var profile = AlphaSceneScaleProfile.Small;

        profile.Scale.Should().Be(AlphaSceneScale.Small);
        profile.OpponentColumns.Should().Be(10);
        profile.OpponentRows.Should().Be(8);
        profile.ProjectileTrails.Should().Be(12);
        profile.Casings.Should().Be(16);
    }

    [Fact]
    public void Large_profile_dominates_small_in_every_axis()
    {
        var small = AlphaSceneScaleProfile.Small;
        var large = AlphaSceneScaleProfile.Large;

        large.OpponentColumns.Should().BeGreaterThan(small.OpponentColumns);
        large.OpponentRows.Should().BeGreaterThan(small.OpponentRows);
        large.ProjectileTrails.Should().BeGreaterThan(small.ProjectileTrails);
        large.Casings.Should().BeGreaterThan(small.Casings);
        large.InstanceCount.Should().BeGreaterThan(small.InstanceCount);
    }

    [Fact]
    public void Instance_count_includes_player_grid_trails_and_casings()
    {
        var profile = AlphaSceneScaleProfile.Small;

        var expected = 1 + (profile.OpponentColumns * profile.OpponentRows)
            + profile.ProjectileTrails + profile.Casings;

        profile.InstanceCount.Should().Be(expected);
    }

    [Theory]
    [InlineData(AlphaSceneScale.Small)]
    [InlineData(AlphaSceneScale.Large)]
    public void For_returns_matching_profile(AlphaSceneScale scale)
    {
        var profile = AlphaSceneScaleProfile.For(scale);

        profile.Scale.Should().Be(scale);
    }

    [Fact]
    public void For_throws_on_unknown_scale()
    {
        Action act = () => AlphaSceneScaleProfile.For((AlphaSceneScale)42);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(1, 1, -1, 0)]
    [InlineData(1, 1, 0, -1)]
    public void Validate_rejects_invalid_dimensions(int columns, int rows, int trails, int casings)
    {
        var profile = new AlphaSceneScaleProfile(AlphaSceneScale.Small, columns, rows, trails, casings);

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_profiles_validate_cleanly()
    {
        AlphaSceneScaleProfile.Small.Invoking(p => p.Validate()).Should().NotThrow();
        AlphaSceneScaleProfile.Large.Invoking(p => p.Validate()).Should().NotThrow();
    }
}
