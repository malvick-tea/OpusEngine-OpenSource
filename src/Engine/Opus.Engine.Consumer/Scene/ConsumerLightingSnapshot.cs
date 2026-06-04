using System;
using System.Numerics;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Frame-wide engine-neutral lighting declared by a consumer scene.</summary>
public sealed record ConsumerLightingSnapshot
{
    /// <summary>Creates a lighting snapshot.</summary>
    public ConsumerLightingSnapshot(
        ConsumerDirectionalLight sun,
        IReadOnlyList<ConsumerLocalLight> localLights,
        ConsumerSkySnapshot sky)
    {
        Sun = sun;
        LocalLights = ConsumerContractValidation.CopyRequiredList(localLights, nameof(localLights));
        Sky = sky;
    }

    /// <summary>Directional sun light.</summary>
    public ConsumerDirectionalLight Sun { get; }

    /// <summary>Local lights visible in the frame.</summary>
    public IReadOnlyList<ConsumerLocalLight> LocalLights { get; }

    /// <summary>Sky and ambient-light settings.</summary>
    public ConsumerSkySnapshot Sky { get; }

    /// <summary>Creates a simple single-sun lighting snapshot useful for smoke consumers.</summary>
    public static ConsumerLightingSnapshot SingleSun(Vector3 sunDirectionWorld) => new(
        new ConsumerDirectionalLight(sunDirectionWorld, new Vector3(1f, 0.96f, 0.88f), 1.0f, CastsShadows: false),
        Array.Empty<ConsumerLocalLight>(),
        new ConsumerSkySnapshot(sunDirectionWorld, new Vector3(0.42f, 0.52f, 0.64f), new Vector3(0.12f, 0.18f, 0.24f), 0f, 0));
}
