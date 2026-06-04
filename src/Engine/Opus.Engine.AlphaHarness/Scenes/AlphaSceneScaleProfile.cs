using System;

namespace Opus.Engine.AlphaHarness.Scenes;

/// <summary>
/// Per-axis instance counts the host injects into the canonical
/// <c>D3D12AlphaFramePlan.Create</c> overload. Values describe the population of one
/// scene-density preset; the renderer turns them into the actual scene matrices.
/// </summary>
/// <param name="Scale">Named preset this profile represents.</param>
/// <param name="OpponentColumns">Number of opponent columns in the repeated-actor grid.</param>
/// <param name="OpponentRows">Number of opponent rows in the repeated-actor grid.</param>
/// <param name="ProjectileTrails">Per-frame transient trail instances along one axis.</param>
/// <param name="Casings">Per-frame transient casing instances along one axis.</param>
public sealed record AlphaSceneScaleProfile(
    AlphaSceneScale Scale,
    int OpponentColumns,
    int OpponentRows,
    int ProjectileTrails,
    int Casings)
{
    /// <summary>Compact scene matching the existing M5/M5.1 smoke shape.</summary>
    public static AlphaSceneScaleProfile Small { get; } = new(
        Scale: AlphaSceneScale.Small,
        OpponentColumns: 10,
        OpponentRows: 8,
        ProjectileTrails: 12,
        Casings: 16);

    /// <summary>Large-map scene sized to stress camera/batching/memory/frame pacing.
    /// 20×16 grid → 320 opponent actors + 36 trails + 48 casings = 405 transient nodes,
    /// well above the M5/M5.1 baseline.</summary>
    public static AlphaSceneScaleProfile Large { get; } = new(
        Scale: AlphaSceneScale.Large,
        OpponentColumns: 20,
        OpponentRows: 16,
        ProjectileTrails: 36,
        Casings: 48);

    /// <summary>Massive scene preset for M12+ stress baselines. 40×30 grid → 1200 opponent
    /// actors + 64 trails + 96 casings = 1361 transient nodes — an order of magnitude
    /// above <see cref="Large"/>, sized so memory and batching regressions surface during
    /// the August hardening pass.</summary>
    public static AlphaSceneScaleProfile Massive { get; } = new(
        Scale: AlphaSceneScale.Massive,
        OpponentColumns: 40,
        OpponentRows: 30,
        ProjectileTrails: 64,
        Casings: 96);

    /// <summary>Resolves a profile from its enum value.</summary>
    public static AlphaSceneScaleProfile For(AlphaSceneScale scale) => scale switch
    {
        AlphaSceneScale.Small => Small,
        AlphaSceneScale.Large => Large,
        AlphaSceneScale.Massive => Massive,
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, "Unknown alpha scene scale."),
    };

    /// <summary>Throws when a custom profile has invalid dimensions.</summary>
    public void Validate()
    {
        if (OpponentColumns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(OpponentColumns), "OpponentColumns must be at least 1.");
        }

        if (OpponentRows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(OpponentRows), "OpponentRows must be at least 1.");
        }

        if (ProjectileTrails < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ProjectileTrails), "ProjectileTrails must be non-negative.");
        }

        if (Casings < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Casings), "Casings must be non-negative.");
        }
    }

    /// <summary>Total expected actor + transient count after the alpha-plan composes the
    /// scene. Includes the player + opponent grid + trails + casings.</summary>
    public int InstanceCount => 1 + (OpponentColumns * OpponentRows) + ProjectileTrails + Casings;
}
