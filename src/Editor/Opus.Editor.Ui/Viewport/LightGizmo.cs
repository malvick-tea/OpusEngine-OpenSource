using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// Builds the world-space line glyph that marks a <see cref="SceneLight"/> in the viewport: a small
/// three-axis star at the light's icon position, plus the light's real reach — a spot light draws its
/// cone out to its range and outer angle, a point light its range ring, a directional light its fixed aim
/// ray — so the authored lighting parameters are visible and editable by eye. Pure — the glyph draws
/// through the existing UI line batch in the <see cref="ViewportLineRole.Light"/> colour, so a light adds
/// no new render concept (ADR-0028 / ADR-0033).
/// </summary>
public static class LightGizmo
{
    /// <summary>Half-length of each star arm, in metres.</summary>
    public const float StarRadiusMeters = 0.4f;

    /// <summary>Length of the aim ray drawn for a directional light (and for a spot with no usable range),
    /// in metres.</summary>
    public const float AimRayMeters = 1.5f;

    /// <summary>Segments in a point light's horizontal range ring.</summary>
    public const int RangeRingSegments = 24;

    /// <summary>Segments in a spot cone's end circle.</summary>
    public const int ConeRimSegments = 16;

    /// <summary>Edge lines from a spot light's position to its cone rim.</summary>
    public const int ConeEdgeCount = 4;

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Appends the glyph lines for <paramref name="light"/> to <paramref name="sink"/>, in
    /// <paramref name="role"/> — <see cref="ViewportLineRole.Light"/> normally, promoted to
    /// <see cref="ViewportLineRole.Selection"/> for the selected light.</summary>
    public static void AppendDrawLines(
        ICollection<ViewportLine> sink, SceneLight light, ViewportLineRole role = ViewportLineRole.Light)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(light);
        Vector3 center = light.Position.ToVector3();
        AppendArm(sink, center, Vector3.UnitX, role);
        AppendArm(sink, center, Vector3.UnitY, role);
        AppendArm(sink, center, Vector3.UnitZ, role);
        switch (light.Kind)
        {
            case SceneLightKind.Point:
                AppendRangeRing(sink, center, light.Range, role);
                break;
            case SceneLightKind.Spot:
                AppendSpotCone(sink, center, Aim(light.Direction.ToVector3()), light, role);
                break;
            default:
                sink.Add(new ViewportLine(center, center + (Aim(light.Direction.ToVector3()) * AimRayMeters), role));
                break;
        }
    }

    private static void AppendArm(ICollection<ViewportLine> sink, Vector3 center, Vector3 axis, ViewportLineRole role)
    {
        Vector3 arm = axis * StarRadiusMeters;
        sink.Add(new ViewportLine(center - arm, center + arm, role));
    }

    /// <summary>A point light's attenuation range as a horizontal ring at the light's height — readable
    /// from the default top-down-ish orbit without flooding the viewport. Skipped for a degenerate range.</summary>
    private static void AppendRangeRing(ICollection<ViewportLine> sink, Vector3 center, float range, ViewportLineRole role)
    {
        if (range <= 0f)
        {
            return;
        }

        var previous = center + new Vector3(range, 0f, 0f);
        for (int i = 1; i <= RangeRingSegments; i++)
        {
            float angle = i * (MathF.Tau / RangeRingSegments);
            var current = center + new Vector3(MathF.Cos(angle) * range, 0f, MathF.Sin(angle) * range);
            sink.Add(new ViewportLine(previous, current, role));
            previous = current;
        }
    }

    /// <summary>A spot light's cone out to its range: the axis ray, four edge lines to the rim, and the
    /// rim circle whose radius follows the outer cone half-angle. A spot with no usable range or angle
    /// falls back to the plain aim ray.</summary>
    private static void AppendSpotCone(
        ICollection<ViewportLine> sink, Vector3 center, Vector3 aim, SceneLight light, ViewportLineRole role)
    {
        if (light.Range <= 0f || light.SpotOuterAngleDegrees <= 0f)
        {
            sink.Add(new ViewportLine(center, center + (aim * AimRayMeters), role));
            return;
        }

        var rimCenter = center + (aim * light.Range);
        float rimRadius = MathF.Tan(light.SpotOuterAngleDegrees * DegreesToRadians) * light.Range;
        var (right, up) = PerpendicularBasis(aim);

        sink.Add(new ViewportLine(center, rimCenter, role));
        for (int i = 0; i < ConeEdgeCount; i++)
        {
            float angle = i * (MathF.Tau / ConeEdgeCount);
            var rim = rimCenter + (((right * MathF.Cos(angle)) + (up * MathF.Sin(angle))) * rimRadius);
            sink.Add(new ViewportLine(center, rim, role));
        }

        var previous = rimCenter + (right * rimRadius);
        for (int i = 1; i <= ConeRimSegments; i++)
        {
            float angle = i * (MathF.Tau / ConeRimSegments);
            var current = rimCenter + (((right * MathF.Cos(angle)) + (up * MathF.Sin(angle))) * rimRadius);
            sink.Add(new ViewportLine(previous, current, role));
            previous = current;
        }
    }

    /// <summary>The light's normalised aim, falling back to straight down for a zero direction.</summary>
    private static Vector3 Aim(Vector3 direction) =>
        direction.LengthSquared() > 0f ? Vector3.Normalize(direction) : -Vector3.UnitY;

    /// <summary>Two unit axes perpendicular to <paramref name="aim"/> spanning the rim plane.</summary>
    private static (Vector3 Right, Vector3 Up) PerpendicularBasis(Vector3 aim)
    {
        var helper = MathF.Abs(Vector3.Dot(aim, Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(aim, helper));
        return (right, Vector3.Cross(aim, right));
    }
}
