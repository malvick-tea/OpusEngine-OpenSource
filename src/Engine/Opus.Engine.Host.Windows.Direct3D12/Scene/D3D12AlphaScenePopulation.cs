using System;

namespace Opus.Engine.Host.Windows.Direct3D12.Scene;

/// <summary>
/// Per-axis instance counts the host hands to <see cref="D3D12AlphaSceneRig"/>. Keeps the
/// host-renderer seam plain data so the AlphaHarness can map its <c>AlphaSceneScale</c>
/// enum to renderer instance counts without coupling renderer code to the harness
/// assembly. The defaults match the existing M5/M5.1 small smoke shape.
/// </summary>
/// <param name="OpponentColumns">Number of opponent columns in the repeated-actor grid.</param>
/// <param name="OpponentRows">Number of opponent rows in the repeated-actor grid.</param>
/// <param name="ProjectileTrails">Per-frame transient trail instances along one axis.</param>
/// <param name="Casings">Per-frame transient casing instances along one axis.</param>
public sealed record D3D12AlphaScenePopulation(
    int OpponentColumns,
    int OpponentRows,
    int ProjectileTrails,
    int Casings)
{
    /// <summary>Default small-scale population (~80 opponents + transients) matching the
    /// existing M5/M5.1 smoke. Used when callers do not override the rig population.</summary>
    public static D3D12AlphaScenePopulation Default { get; } = new(
        OpponentColumns: 10,
        OpponentRows: 8,
        ProjectileTrails: 12,
        Casings: 16);

    /// <summary>Throws when any dimension is invalid.</summary>
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
}
