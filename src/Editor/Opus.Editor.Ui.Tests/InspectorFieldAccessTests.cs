using FluentAssertions;
using Opus.Editor.Core;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class InspectorFieldAccessTests
{
    [Theory]
    [InlineData(InspectorField.PositionX)]
    [InlineData(InspectorField.PositionY)]
    [InlineData(InspectorField.PositionZ)]
    [InlineData(InspectorField.RotationX)]
    [InlineData(InspectorField.RotationY)]
    [InlineData(InspectorField.RotationZ)]
    [InlineData(InspectorField.ScaleX)]
    [InlineData(InspectorField.ScaleY)]
    [InlineData(InspectorField.ScaleZ)]
    public void Every_transform_field_round_trips(InspectorField field)
    {
        var applied = InspectorFieldAccess.Apply(EditorTransform.Identity, field, 7.5f);

        applied.Should().NotBeNull();
        InspectorFieldAccess.Read(applied!.Value, field).Should().Be(7.5f);
    }

    [Fact]
    public void Non_transform_fields_do_not_apply_to_nodes()
    {
        InspectorFieldAccess.Read(EditorTransform.Identity, InspectorField.Intensity).Should().BeNull();
        InspectorFieldAccess.Apply(EditorTransform.Identity, InspectorField.ColorR, 1f).Should().BeNull();
    }

    [Theory]
    [InlineData(InspectorField.PositionY)]
    [InlineData(InspectorField.DirectionZ)]
    [InlineData(InspectorField.ColorG)]
    [InlineData(InspectorField.Intensity)]
    [InlineData(InspectorField.Range)]
    [InlineData(InspectorField.SpotInner)]
    [InlineData(InspectorField.SpotOuter)]
    public void Every_spot_light_field_round_trips(InspectorField field)
    {
        var light = SceneLight.CreateSpot("torch");

        var applied = InspectorFieldAccess.Apply(light, field, 0.25f);

        applied.Should().NotBeNull();
        InspectorFieldAccess.Read(applied!, field).Should().Be(0.25f);
    }

    [Fact]
    public void A_directional_light_rejects_position_and_range()
    {
        var sun = SceneLight.CreateDirectional("sun");

        InspectorFieldAccess.Read(sun, InspectorField.PositionX).Should().BeNull();
        InspectorFieldAccess.Apply(sun, InspectorField.Range, 5f).Should().BeNull();
    }

    [Fact]
    public void A_point_light_rejects_direction_and_cone_angles()
    {
        var lamp = SceneLight.CreatePoint("lamp");

        InspectorFieldAccess.Read(lamp, InspectorField.DirectionX).Should().BeNull();
        InspectorFieldAccess.Apply(lamp, InspectorField.SpotInner, 10f).Should().BeNull();
    }

    [Fact]
    public void Applying_a_field_changes_only_that_field()
    {
        var light = SceneLight.CreateSpot("torch");

        var applied = InspectorFieldAccess.Apply(light, InspectorField.ColorR, 0.1f)!;

        applied.Color.X.Should().Be(0.1f);
        applied.Color.Y.Should().Be(light.Color.Y);
        applied.Intensity.Should().Be(light.Intensity);
        applied.Range.Should().Be(light.Range);
    }
}
