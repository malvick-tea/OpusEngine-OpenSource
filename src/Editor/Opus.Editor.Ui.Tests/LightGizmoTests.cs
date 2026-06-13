using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class LightGizmoTests
{
    private static List<ViewportLine> GlyphFor(SceneLight light)
    {
        var sink = new List<ViewportLine>();
        LightGizmo.AppendDrawLines(sink, light);
        return sink;
    }

    [Fact]
    public void Point_light_draws_its_star_and_a_horizontal_range_ring()
    {
        var light = SceneLight.CreatePoint("lamp") with { Position = new Float3(0f, 2f, 0f) };

        var glyph = GlyphFor(light);

        glyph.Should().HaveCount(3 + LightGizmo.RangeRingSegments);
        glyph.Should().OnlyContain(l => l.Role == ViewportLineRole.Light);
        var ring = glyph.Skip(3).ToList();
        ring.Should().OnlyContain(
            l => l.A.Y == 2f && l.B.Y == 2f, "the range ring lies in the light's horizontal plane");
        Vector3.Distance(ring[0].A, new Vector3(0f, 2f, 0f)).Should().BeApproximately(
            SceneLight.DefaultRangeMeters, 1e-3f, "the ring sits at the attenuation range");
    }

    [Fact]
    public void A_zero_range_point_light_draws_only_its_star()
    {
        GlyphFor(SceneLight.CreatePoint("lamp") with { Range = 0f }).Should().HaveCount(3);
    }

    [Fact]
    public void Directional_light_adds_an_aim_ray()
    {
        GlyphFor(SceneLight.CreateDirectional("sun")).Should().HaveCount(4, "three star arms plus one aim ray");
    }

    [Fact]
    public void Spot_light_draws_its_cone_to_the_authored_range_and_angle()
    {
        var light = SceneLight.CreateSpot("torch") with { Position = new Float3(0f, 5f, 0f) };

        var glyph = GlyphFor(light);

        glyph.Should().HaveCount(
            3 + 1 + LightGizmo.ConeEdgeCount + LightGizmo.ConeRimSegments,
            "star arms, the axis ray, the cone edges, and the rim circle");

        // The default spot aims straight down from (0,5,0) with a 10 m range, so the axis ray ends at the
        // rim centre (0,-5,0) and the rim circle has radius tan(30 deg) * 10.
        var axis = glyph[3];
        axis.A.Should().Be(new Vector3(0f, 5f, 0f));
        axis.B.Should().Be(new Vector3(0f, -5f, 0f));
        float expectedRimRadius = MathF.Tan(SceneLight.DefaultSpotOuterAngleDegrees * MathF.PI / 180f)
            * SceneLight.DefaultRangeMeters;
        var rimPoint = glyph[^1].A;
        Vector3.Distance(rimPoint, new Vector3(0f, -5f, 0f)).Should().BeApproximately(expectedRimRadius, 1e-3f);
    }

    [Fact]
    public void A_spot_with_no_usable_range_falls_back_to_the_plain_aim_ray()
    {
        GlyphFor(SceneLight.CreateSpot("torch") with { Range = 0f }).Should().HaveCount(4);
    }

    [Fact]
    public void Star_arms_stay_within_the_star_radius_of_the_light_position()
    {
        var light = SceneLight.CreatePoint("lamp") with { Position = new Float3(2f, 3f, 4f), Range = 0f };
        var center = new Vector3(2f, 3f, 4f);
        const float tolerance = 0.001f;

        GlyphFor(light).Should().OnlyContain(l =>
            Vector3.Distance(l.A, center) <= LightGizmo.StarRadiusMeters + tolerance &&
            Vector3.Distance(l.B, center) <= LightGizmo.StarRadiusMeters + tolerance);
    }

    [Fact]
    public void Aim_ray_starts_at_the_light_and_follows_its_direction()
    {
        var light = SceneLight.CreateDirectional("sun") with { Direction = new Float3(1f, 0f, 0f) };

        var ray = GlyphFor(light).Single(l => l.A == Vector3.Zero);

        ray.B.Should().Be(new Vector3(LightGizmo.AimRayMeters, 0f, 0f));
    }

    [Fact]
    public void Zero_direction_aim_ray_falls_back_to_pointing_down()
    {
        var light = SceneLight.CreateDirectional("sun") with { Direction = Float3.Zero };

        var ray = GlyphFor(light).Single(l => l.A == Vector3.Zero);

        ray.B.Should().Be(new Vector3(0f, -LightGizmo.AimRayMeters, 0f));
    }

    [Fact]
    public void Light_role_maps_to_the_light_colour()
    {
        EditorViewportColors.ForRole(ViewportLineRole.Light).Should().Be(EditorViewportColors.Light);
    }
}
