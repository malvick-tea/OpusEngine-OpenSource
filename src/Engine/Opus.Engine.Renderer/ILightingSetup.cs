using System.Numerics;

namespace Opus.Engine.Renderer;

/// <summary>
/// Directional light (the sun) — always present in a scene. Drives the cascaded
/// shadow-map pass (per ADR-0018). Sky / atmospheric parameters live separately on
/// <see cref="SkySetup"/>.
/// </summary>
public readonly record struct DirectionalLight(
    Vector3 DirectionWorld,
    Vector3 Colour,
    float Intensity,
    bool CastsShadows);

/// <summary>Local light — point, spot, or area. Tile-culled per pass into the per-tile
/// light list. Up to 1024 supported per scene; ~50 per tile after cull (ADR-0018).</summary>
public readonly record struct LocalLight(
    LocalLightKind Kind,
    Vector3 PositionWorld,
    Vector3 DirectionWorld,   // unused for Point lights
    Vector3 Colour,
    float Intensity,
    float Range,
    float SpotInnerAngleRadians,
    float SpotOuterAngleRadians,
    bool CastsShadows);

public enum LocalLightKind : byte
{
    Point = 0,
    Spot = 1,
    Area = 2,
}

/// <summary>Sky + IBL environment parameters. The renderer samples this for ambient
/// diffuse (via spherical harmonics) and specular reflections (via a filtered
/// environment map).</summary>
public readonly record struct SkySetup(
    Vector3 SunDirectionWorld,
    Vector3 ZenithColour,
    Vector3 HorizonColour,
    float ExposureEv,
    int EnvironmentMapHandle);

/// <summary>Frame-wide lighting state. Like <see cref="CameraSetup"/>, immutable once
/// captured at <c>BeginFrame</c>; late mutations apply to the next frame.</summary>
public sealed record LightingSetup(
    DirectionalLight Sun,
    System.Collections.Generic.IReadOnlyList<LocalLight> LocalLights,
    SkySetup Sky);
