using System.Numerics;
using FluentAssertions;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Foundation.Tests.Geometry;

public sealed class FrustumTests
{
    [Fact]
    public void Intersects_keeps_aabb_at_origin()
    {
        var vp = BuildPerspective();
        var frustum = Frustum.FromViewProjection(vp);

        // Unit cube at origin: well inside a camera looking from +Z.
        var inside = new Aabb(new Vector3(-0.5f), new Vector3(0.5f));
        frustum.Intersects(inside).Should().BeTrue();
    }

    [Fact]
    public void Intersects_culls_aabb_far_to_the_side()
    {
        var vp = BuildPerspective();
        var frustum = Frustum.FromViewProjection(vp);

        // Way off-screen to the right — past the right plane.
        var offRight = new Aabb(new Vector3(100f, -0.5f, -0.5f), new Vector3(101f, 0.5f, 0.5f));
        frustum.Intersects(offRight).Should().BeFalse();

        // Way off-screen up.
        var offUp = new Aabb(new Vector3(-0.5f, 100f, -0.5f), new Vector3(0.5f, 101f, 0.5f));
        frustum.Intersects(offUp).Should().BeFalse();
    }

    [Fact]
    public void Intersects_culls_aabb_behind_camera()
    {
        var vp = BuildPerspective();
        var frustum = Frustum.FromViewProjection(vp);

        // Camera looks from (0, 0, 10) toward origin (-Z direction). Anything past origin
        // on +Z side from the camera (still toward camera) is behind: e.g. (0, 0, 50).
        var behind = new Aabb(new Vector3(-0.5f, -0.5f, 49f), new Vector3(0.5f, 0.5f, 51f));
        frustum.Intersects(behind).Should().BeFalse();
    }

    [Fact]
    public void Intersects_culls_aabb_past_far_plane()
    {
        var vp = BuildPerspective();
        var frustum = Frustum.FromViewProjection(vp);

        // Past the far plane (camera at z=10, far=100, so z < -90 in world space is past far).
        var farAway = new Aabb(new Vector3(-0.5f, -0.5f, -200f), new Vector3(0.5f, 0.5f, -199f));
        frustum.Intersects(farAway).Should().BeFalse();
    }

    [Fact]
    public void Intersects_keeps_aabb_straddling_near_plane()
    {
        var vp = BuildPerspective();
        var frustum = Frustum.FromViewProjection(vp);

        // AABB straddles the near plane (camera at z=10, near=0.1, so near plane at z=9.9).
        var straddle = new Aabb(new Vector3(-0.5f, -0.5f, 9.5f), new Vector3(0.5f, 0.5f, 11f));
        frustum.Intersects(straddle).Should().BeTrue();
    }

    private static Matrix4x4 BuildPerspective()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 10f), Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 16f / 9f, 0.1f, 100f);
        return view * proj;
    }
}
