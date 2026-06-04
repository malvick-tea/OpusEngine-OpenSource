using System.Numerics;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Engine-neutral sky and ambient-light declaration consumed by host renderers.</summary>
public readonly record struct ConsumerSkySnapshot(
    Vector3 SunDirectionWorld,
    Vector3 ZenithColour,
    Vector3 HorizonColour,
    float ExposureEv,
    int EnvironmentMapHandle);
