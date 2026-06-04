using System;

namespace Opus.Engine.Consumer.Assets;

/// <summary>
/// Stable, engine-neutral id for an asset declared by a consumer scene. The id has no
/// game meaning; host adapters map it to backend-owned GPU resources or skip it with a
/// diagnostic when unsupported.
/// </summary>
public readonly record struct ConsumerAssetId
{
    /// <summary>Stable string value for <see cref="PrimarySceneModel"/>. Exposed as a
    /// constant so manifests, tests, and host telemetry compare against one source of
    /// truth instead of repeating the literal.</summary>
    public const string PrimarySceneModelValue = "primary-scene-model";

    /// <summary>Primary scene model slot consumed by the current alpha host bridge.</summary>
    public static ConsumerAssetId PrimarySceneModel { get; } = new(PrimarySceneModelValue);

    /// <summary>Creates an asset id from a stable non-empty value.</summary>
    public ConsumerAssetId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Consumer asset id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Stable id value used for equality and diagnostics.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
