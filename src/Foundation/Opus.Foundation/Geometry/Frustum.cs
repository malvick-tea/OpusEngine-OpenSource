using System.Numerics;

namespace Opus.Foundation.Geometry;

/// <summary>
/// 6-plane view frustum extracted from a row-vector view-projection matrix
/// (System.Numerics convention: <c>p_clip = p_world × M</c>). Plane normals point
/// INTO the frustum; the inside half-space satisfies <c>dot(n, p) + d ≥ 0</c>.
///
/// Built for D3D NDC z-range [0, 1] — the near plane uses M's column 2 alone, the far
/// plane uses column 3 - column 2. OpenGL's symmetric [-1, 1] z-range would need a
/// slightly different near plane.
/// </summary>
public readonly struct Frustum
{
    public readonly Plane Left;
    public readonly Plane Right;
    public readonly Plane Bottom;
    public readonly Plane Top;
    public readonly Plane Near;
    public readonly Plane Far;

    private Frustum(Plane left, Plane right, Plane bottom, Plane top, Plane near, Plane far)
    {
        Left = left;
        Right = right;
        Bottom = bottom;
        Top = top;
        Near = near;
        Far = far;
    }

    /// <summary>
    /// Extracts the 6 frustum planes via the Gribb-Hartmann method on a row-vector
    /// view-projection matrix. Each plane (n, d) is in world space; planes are not
    /// normalised — fine for sign-only inside/outside tests.
    /// </summary>
    public static Frustum FromViewProjection(Matrix4x4 vp)
    {
        // Columns of the matrix in "row vector × M = clip" convention. Column 0 generates
        // x_clip when dotted with the row vector; column 3 generates w_clip; etc.
        var c0 = new Vector4(vp.M11, vp.M21, vp.M31, vp.M41);
        var c1 = new Vector4(vp.M12, vp.M22, vp.M32, vp.M42);
        var c2 = new Vector4(vp.M13, vp.M23, vp.M33, vp.M43);
        var c3 = new Vector4(vp.M14, vp.M24, vp.M34, vp.M44);

        // Inside-frustum tests:
        //   left:    x + w ≥ 0     →   (c0 + c3)
        //   right:   w - x ≥ 0     →   (c3 - c0)
        //   bottom:  y + w ≥ 0     →   (c1 + c3)
        //   top:     w - y ≥ 0     →   (c3 - c1)
        //   near:    z ≥ 0         →   (c2)            [D3D NDC z in [0,1]]
        //   far:     w - z ≥ 0     →   (c3 - c2)
        return new Frustum(
            PlaneFromVec4(c0 + c3),
            PlaneFromVec4(c3 - c0),
            PlaneFromVec4(c1 + c3),
            PlaneFromVec4(c3 - c1),
            PlaneFromVec4(c2),
            PlaneFromVec4(c3 - c2));
    }

    /// <summary>
    /// True if <paramref name="aabb"/> is fully or partially inside the frustum.
    /// Returns false only when the AABB is entirely outside at least one plane.
    /// Conservative — may keep AABBs that overlap a corner outside the frustum.
    /// </summary>
    public bool Intersects(Aabb aabb)
    {
        return InsidePlane(Left, aabb)
            && InsidePlane(Right, aabb)
            && InsidePlane(Bottom, aabb)
            && InsidePlane(Top, aabb)
            && InsidePlane(Near, aabb)
            && InsidePlane(Far, aabb);
    }

    private static bool InsidePlane(Plane plane, Aabb aabb)
    {
        // Pick the AABB corner that's most-positive along the plane normal — if even THAT
        // corner is on the outside side (signed distance < 0), the entire AABB is outside.
        var n = plane.Normal;
        var corner = new Vector3(
            n.X >= 0 ? aabb.Max.X : aabb.Min.X,
            n.Y >= 0 ? aabb.Max.Y : aabb.Min.Y,
            n.Z >= 0 ? aabb.Max.Z : aabb.Min.Z);
        return Vector3.Dot(n, corner) + plane.D >= 0;
    }

    private static Plane PlaneFromVec4(Vector4 v) => new(new Vector3(v.X, v.Y, v.Z), v.W);
}

/// <summary>
/// Implicit plane <c>dot(Normal, p) + D = 0</c>. Inside half-space: > 0.
/// </summary>
public readonly record struct Plane(Vector3 Normal, float D);
