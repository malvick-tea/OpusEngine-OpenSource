using System;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Renderer-neutral content counts shown to testers in the overlay.</summary>
public sealed record DiagnosticContentSnapshot(
    int SubmittedDrawItems,
    int SceneInstanceCount,
    string AssetSource,
    bool UsesProceduralFallback)
{
    /// <summary>Creates a validated content snapshot.</summary>
    public static DiagnosticContentSnapshot Create(
        int submittedDrawItems,
        int sceneInstanceCount,
        string assetSource,
        bool usesProceduralFallback)
    {
        if (submittedDrawItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(submittedDrawItems), "Draw item count must not be negative.");
        }

        if (sceneInstanceCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sceneInstanceCount), "Scene instance count must not be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(assetSource);
        return new DiagnosticContentSnapshot(
            submittedDrawItems,
            sceneInstanceCount,
            assetSource,
            usesProceduralFallback);
    }
}
