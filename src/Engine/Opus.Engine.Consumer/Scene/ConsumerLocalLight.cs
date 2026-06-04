using System.Numerics;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Engine-neutral local light declaration carried by a consumer lighting snapshot.</summary>
public readonly record struct ConsumerLocalLight(
    ConsumerLocalLightKind Kind,
    Vector3 PositionWorld,
    Vector3 DirectionWorld,
    Vector3 Colour,
    float Intensity,
    float Range,
    float SpotInnerAngleRadians,
    float SpotOuterAngleRadians,
    bool CastsShadows);
