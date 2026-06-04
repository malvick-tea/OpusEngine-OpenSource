using System.Numerics;
using FluentAssertions;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Foundation.Tests.Geometry;

public sealed class AabbTests
{
    [Fact]
    public void FromPoints_returns_tight_aabb()
    {
        var pts = new[]
        {
            new Vector3(-1f, 2f, 3f),
            new Vector3(4f, -5f, 6f),
            new Vector3(0f, 0f, 0f),
        };

        var aabb = Aabb.FromPoints(pts);

        aabb.Min.Should().Be(new Vector3(-1f, -5f, 0f));
        aabb.Max.Should().Be(new Vector3(4f, 2f, 6f));
    }

    [Fact]
    public void FromPoints_returns_empty_for_empty_input()
    {
        var aabb = Aabb.FromPoints(System.Array.Empty<Vector3>());

        aabb.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Union_combines_two_aabbs()
    {
        var a = new Aabb(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f));
        var b = new Aabb(new Vector3(-1f, 2f, 0.5f), new Vector3(0.5f, 3f, 2f));

        var u = a.Union(b);

        u.Min.Should().Be(new Vector3(-1f, 0f, 0f));
        u.Max.Should().Be(new Vector3(1f, 3f, 2f));
    }

    [Fact]
    public void Transform_under_translation_shifts_aabb_by_the_same_offset()
    {
        var aabb = new Aabb(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f));
        var t = Matrix4x4.CreateTranslation(5f, -2f, 3f);

        var moved = aabb.Transform(t);

        moved.Min.Should().Be(new Vector3(4f, -3f, 2f));
        moved.Max.Should().Be(new Vector3(6f, -1f, 4f));
    }

    [Fact]
    public void Transform_under_90deg_z_rotation_swaps_xy_extents()
    {
        var aabb = new Aabb(new Vector3(0f, 0f, 0f), new Vector3(2f, 1f, 1f));
        var r = Matrix4x4.CreateRotationZ(MathF.PI / 2f);

        var rotated = aabb.Transform(r);

        rotated.Min.X.Should().BeApproximately(-1f, 1e-5f);
        rotated.Max.X.Should().BeApproximately(0f, 1e-5f);
        rotated.Min.Y.Should().BeApproximately(0f, 1e-5f);
        rotated.Max.Y.Should().BeApproximately(2f, 1e-5f);
    }
}
