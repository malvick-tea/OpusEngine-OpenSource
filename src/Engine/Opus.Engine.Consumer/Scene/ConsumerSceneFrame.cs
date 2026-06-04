using System;

namespace Opus.Engine.Consumer.Scene;

/// <summary>
/// Complete scene declaration for a render frame: draw list, cameras, and lighting.
/// Hosts adapt this record into their backend-specific renderer without learning the
/// game or tool rules that produced it.
/// </summary>
public sealed record ConsumerSceneFrame
{
    /// <summary>Creates a validated scene frame.</summary>
    public ConsumerSceneFrame(
        IReadOnlyList<ConsumerDrawItem> drawItems,
        ConsumerCameraSet cameras,
        ConsumerLightingSnapshot lighting)
    {
        DrawItems = ConsumerContractValidation.CopyRequiredList(drawItems, nameof(drawItems));
        Cameras = cameras ?? throw new ArgumentNullException(nameof(cameras));
        Lighting = lighting ?? throw new ArgumentNullException(nameof(lighting));
    }

    /// <summary>Consumer-declared draw items for this frame.</summary>
    public IReadOnlyList<ConsumerDrawItem> DrawItems { get; }

    /// <summary>Consumer-declared camera set.</summary>
    public ConsumerCameraSet Cameras { get; }

    /// <summary>Consumer-declared lighting.</summary>
    public ConsumerLightingSnapshot Lighting { get; }
}
