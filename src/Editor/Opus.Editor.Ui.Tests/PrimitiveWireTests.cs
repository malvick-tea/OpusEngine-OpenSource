using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class PrimitiveWireTests
{
    private static List<ViewportLine> LinesFor(ScenePrimitiveKind kind, Matrix4x4? world = null)
    {
        var sink = new List<ViewportLine>();
        PrimitiveWire.AppendDrawLines(sink, kind, world ?? Matrix4x4.Identity, ViewportLineRole.NodeBounds);
        return sink;
    }

    [Theory]
    [InlineData(ScenePrimitiveKind.Cube, 12)]
    [InlineData(ScenePrimitiveKind.Sphere, 3 * PrimitiveWire.CircleSegments)]
    [InlineData(ScenePrimitiveKind.Cylinder, (2 * PrimitiveWire.CircleSegments) + 4)]
    [InlineData(ScenePrimitiveKind.Plane, 6)]
    [InlineData(ScenePrimitiveKind.Cone, PrimitiveWire.CircleSegments + 4)]
    public void Each_shape_emits_its_expected_line_count(ScenePrimitiveKind kind, int expected)
    {
        LinesFor(kind).Should().HaveCount(expected);
    }

    [Fact]
    public void Lines_carry_the_requested_role()
    {
        var sink = new List<ViewportLine>();

        PrimitiveWire.AppendDrawLines(sink, ScenePrimitiveKind.Cube, Matrix4x4.Identity, ViewportLineRole.Selection);

        sink.Should().OnlyContain(l => l.Role == ViewportLineRole.Selection);
    }

    [Fact]
    public void The_world_transform_moves_every_point()
    {
        var world = Matrix4x4.CreateTranslation(10f, 0f, 0f);

        var lines = LinesFor(ScenePrimitiveKind.Sphere, world);

        lines.Should().OnlyContain(l => l.A.X > 9f && l.B.X > 9f);
    }

    [Fact]
    public void The_cube_shape_rotates_with_the_node_unlike_an_aligned_bounds_box()
    {
        var world = Matrix4x4.CreateRotationY(MathF.PI / 4f);

        var lines = LinesFor(ScenePrimitiveKind.Cube, world);

        // A 45-degree yaw pushes the rotated corners out to sqrt(0.5) on X — an axis-aligned box would
        // keep every X at exactly +-0.5.
        lines.Max(l => MathF.Max(MathF.Abs(l.A.X), MathF.Abs(l.B.X))).Should().BeGreaterThan(0.6f);
    }

    [Fact]
    public void Cone_slant_lines_meet_at_the_apex()
    {
        var lines = LinesFor(ScenePrimitiveKind.Cone);
        var apex = new Vector3(0f, 0.5f, 0f);

        lines.Count(l => l.B == apex || l.A == apex).Should().Be(4);
    }

    [Fact]
    public void Sphere_points_all_sit_on_the_half_meter_radius()
    {
        var lines = LinesFor(ScenePrimitiveKind.Sphere);

        lines.Should().OnlyContain(l =>
            MathF.Abs(l.A.Length() - 0.5f) < 1e-4f && MathF.Abs(l.B.Length() - 0.5f) < 1e-4f);
    }

    [Theory]
    [InlineData(ScenePrimitiveKind.Cube, 0.5f)]
    [InlineData(ScenePrimitiveKind.Sphere, 0.5f)]
    [InlineData(ScenePrimitiveKind.Cylinder, 0.5f)]
    [InlineData(ScenePrimitiveKind.Cone, 0.5f)]
    public void Volumetric_local_bounds_are_the_half_unit_box(ScenePrimitiveKind kind, float halfExtent)
    {
        var bounds = PrimitiveWire.LocalBounds(kind);

        bounds.Min.Should().Be(new Vector3(-halfExtent));
        bounds.Max.Should().Be(new Vector3(halfExtent));
    }

    [Fact]
    public void The_plane_gets_a_thin_pick_slab_instead_of_a_flat_box()
    {
        var bounds = PrimitiveWire.LocalBounds(ScenePrimitiveKind.Plane);

        bounds.Min.Y.Should().BeApproximately(-PrimitiveWire.PlanePickHalfThickness, 1e-6f);
        bounds.Max.Y.Should().BeApproximately(PrimitiveWire.PlanePickHalfThickness, 1e-6f);
        bounds.Max.X.Should().Be(0.5f);
    }
}
