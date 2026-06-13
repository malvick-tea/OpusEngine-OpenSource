using System.Numerics;
using FluentAssertions;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class RayAabbTests
{
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    [Fact]
    public void Hits_a_box_in_front_at_the_entry_distance()
    {
        var ray = new Ray(new Vector3(0f, 0f, -5f), Vector3.UnitZ);

        RayAabb.Intersects(ray, UnitBox, out float distance).Should().BeTrue();
        distance.Should().BeApproximately(4f, 1e-4f);
    }

    [Fact]
    public void Misses_a_box_behind_the_origin()
    {
        var ray = new Ray(new Vector3(0f, 0f, 5f), Vector3.UnitZ);

        RayAabb.Intersects(ray, UnitBox, out _).Should().BeFalse();
    }

    [Fact]
    public void Misses_a_box_to_the_side()
    {
        var ray = new Ray(new Vector3(5f, 0f, -5f), Vector3.UnitZ);

        RayAabb.Intersects(ray, UnitBox, out _).Should().BeFalse();
    }

    [Fact]
    public void Origin_inside_the_box_is_a_zero_distance_hit()
    {
        var ray = new Ray(Vector3.Zero, Vector3.UnitZ);

        RayAabb.Intersects(ray, UnitBox, out float distance).Should().BeTrue();
        distance.Should().Be(0f);
    }

    [Fact]
    public void An_empty_box_never_hits()
    {
        var ray = new Ray(new Vector3(0f, 0f, -5f), Vector3.UnitZ);

        RayAabb.Intersects(ray, Aabb.Empty, out _).Should().BeFalse();
    }
}
