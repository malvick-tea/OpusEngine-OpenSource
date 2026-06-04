using System.Numerics;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Engine-neutral directional light, typically the sun for a frame.</summary>
public readonly record struct ConsumerDirectionalLight(
    Vector3 DirectionWorld,
    Vector3 Colour,
    float Intensity,
    bool CastsShadows);
