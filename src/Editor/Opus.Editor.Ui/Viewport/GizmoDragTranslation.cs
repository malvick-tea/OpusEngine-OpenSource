using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>
/// Resolves how far along an axis a translate drag has reached: the parameter of the point on the axis line
/// closest to the pointer pick ray (the standard skew-line closest-point solve). Taking the difference
/// between this parameter at grab time and now gives the world-space slide along the axis, so the grabbed
/// point stays under the cursor independent of zoom. Pure math; the controller feeds it a pick ray from the
/// orbit camera.
/// </summary>
public static class GizmoDragTranslation
{
    private const float MinDenominator = 1e-6f;

    /// <summary>Returns the parameter <c>s</c> of the closest point <c>axisOrigin + s · axisUnit</c> on the
    /// axis line to <paramref name="ray"/>. False when the ray is nearly parallel to the axis (the solve is
    /// ill-conditioned and the caller should hold the last value). Both <paramref name="axisUnit"/> and the
    /// ray direction are expected to be unit length.</summary>
    public static bool TryResolveAxisParameter(Ray ray, Vector3 axisOrigin, Vector3 axisUnit, out float parameter)
    {
        var originDelta = axisOrigin - ray.Origin;
        float axisDotRay = Vector3.Dot(axisUnit, ray.Direction);
        float denominator = 1f - (axisDotRay * axisDotRay);
        if (denominator < MinDenominator)
        {
            parameter = 0f;
            return false;
        }

        float axisDotDelta = Vector3.Dot(axisUnit, originDelta);
        float rayDotDelta = Vector3.Dot(ray.Direction, originDelta);
        parameter = ((axisDotRay * rayDotDelta) - axisDotDelta) / denominator;
        return true;
    }
}
