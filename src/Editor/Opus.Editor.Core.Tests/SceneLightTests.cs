using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class SceneLightTests
{
    [Fact]
    public void Create_directional_defaults_to_a_downward_sun()
    {
        var light = SceneLight.CreateDirectional("sun");

        light.Id.Should().Be(SceneLightId.None);
        light.Name.Should().Be("sun");
        light.Kind.Should().Be(SceneLightKind.Directional);
        light.Color.Should().Be(SceneLight.DefaultColor);
        light.Intensity.Should().Be(SceneLight.DefaultIntensity);
        light.Direction.Should().Be(SceneLight.DefaultDirection);
    }

    [Fact]
    public void Create_point_carries_a_range_but_no_cone()
    {
        var light = SceneLight.CreatePoint("lamp");

        light.Kind.Should().Be(SceneLightKind.Point);
        light.Range.Should().Be(SceneLight.DefaultRangeMeters);
        light.SpotInnerAngleDegrees.Should().Be(0f);
        light.SpotOuterAngleDegrees.Should().Be(0f);
    }

    [Fact]
    public void Create_spot_carries_range_and_cone()
    {
        var light = SceneLight.CreateSpot("torch");

        light.Kind.Should().Be(SceneLightKind.Spot);
        light.Range.Should().Be(SceneLight.DefaultRangeMeters);
        light.SpotInnerAngleDegrees.Should().Be(SceneLight.DefaultSpotInnerAngleDegrees);
        light.SpotOuterAngleDegrees.Should().Be(SceneLight.DefaultSpotOuterAngleDegrees);
    }

    [Fact]
    public void With_id_and_with_name_return_modified_copies()
    {
        var light = SceneLight.CreatePoint("lamp");

        var assigned = light.WithId(new SceneLightId(4)).WithName("key");

        assigned.Id.Should().Be(new SceneLightId(4));
        assigned.Name.Should().Be("key");
        light.Id.Should().Be(SceneLightId.None, "the original is unchanged");
    }

    [Fact]
    public void None_id_is_not_valid()
    {
        SceneLightId.None.IsValid.Should().BeFalse();
        new SceneLightId(1).IsValid.Should().BeTrue();
    }
}
