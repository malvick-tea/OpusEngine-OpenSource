using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Scene;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Pure function that composes one flat <see cref="SceneNodeDraw"/> list per
/// frame for the Garage scene: static fixtures (floor) → instantiated tank template
/// (player + opponents with per-school camo) → projectile-trail cubes → shell heads
/// (one full <see cref="GarageSceneAssets.ShellTemplate"/> copy per in-flight AP round).
/// </summary>
/// <remarks>
/// Extracted from <see cref="GarageSceneController"/> in Phase 7 so the controller stops
/// owning both orchestration state AND the draw-list permutation logic. The builder is
/// stateless and side-effect-free — every input arrives via the two <c>readonly record
/// struct</c> bundles below, every output is a fresh allocated list. Pre-sizing the list
/// once avoids per-frame realloc churn.
/// </remarks>
public static class GarageSceneDrawBuilder
{
    /// <summary>Tank-side draw inputs. <see cref="Opponents"/> may be null/empty;
    /// <see cref="OpponentTints"/> aligns 1:1 (entries past its end fall back to
    /// <see cref="Vector4.One"/>).</summary>
    public readonly record struct TankInstancesInput(
        Matrix4x4 PlayerWorld,
        Vector4 PlayerTint,
        IReadOnlyList<Matrix4x4>? Opponents,
        IReadOnlyList<Vector4>? OpponentTints);

    /// <summary>Per-frame transient draw inputs: projectile trail-echo cubes, shell heads
    /// (one full <see cref="ShellTemplate"/> copy per matrix), and ejected casings (one
    /// cylinder per matrix). Each section is independently nullable — the builder skips
    /// the section when its list is null/empty or its mesh index is negative.</summary>
    public readonly record struct TransientsInput(
        IReadOnlyList<Matrix4x4>? CubeTrail,
        int CubeMeshIndex,
        IReadOnlyList<SceneNodeDraw>? ShellTemplate,
        IReadOnlyList<Matrix4x4>? ShellHeads,
        IReadOnlyList<Matrix4x4>? Casings,
        int CasingMeshIndex);

    public static IReadOnlyList<SceneNodeDraw> Build(
        IReadOnlyList<SceneNodeDraw> tankTemplate,
        IReadOnlyList<SceneNodeDraw> staticDraws,
        in TankInstancesInput tanks,
        in TransientsInput transients)
    {
        ArgumentNullException.ThrowIfNull(tankTemplate);
        ArgumentNullException.ThrowIfNull(staticDraws);

        var (instances, tints) = BuildTankInstanceList(in tanks);
        var tankDraws = SceneNodeDrawTransformer.Instantiate(tankTemplate, instances, tints);
        var trailCount = transients.CubeTrail?.Count ?? 0;
        var shellHeadCount = transients.ShellHeads?.Count ?? 0;
        var shellPerInstance = transients.ShellTemplate?.Count ?? 0;
        var casingCount = transients.Casings?.Count ?? 0;

        var all = new List<SceneNodeDraw>(
            staticDraws.Count + tankDraws.Count + trailCount + (shellHeadCount * shellPerInstance) + casingCount);
        for (var i = 0; i < staticDraws.Count; i++)
        {
            all.Add(staticDraws[i]);
        }

        all.AddRange(tankDraws);
        AppendInstancedMesh(all, transients.CubeTrail, transients.CubeMeshIndex);
        AppendShellHeads(all, in transients);
        AppendInstancedMesh(all, transients.Casings, transients.CasingMeshIndex);
        return all;
    }

    private static void AppendInstancedMesh(List<SceneNodeDraw> sink, IReadOnlyList<Matrix4x4>? instances, int meshIndex)
    {
        if (instances is null || meshIndex < 0)
        {
            return;
        }

        for (var i = 0; i < instances.Count; i++)
        {
            sink.Add(new SceneNodeDraw(meshIndex, instances[i]));
        }
    }

    private static void AppendShellHeads(List<SceneNodeDraw> sink, in TransientsInput transients)
    {
        if (transients.ShellTemplate is null || transients.ShellHeads is not { Count: > 0 })
        {
            return;
        }

        sink.AddRange(SceneNodeDrawTransformer.Instantiate(transients.ShellTemplate, transients.ShellHeads));
    }

    private static (Matrix4x4[] Worlds, Vector4[] Tints) BuildTankInstanceList(in TankInstancesInput tanks)
    {
        var opponentCount = tanks.Opponents?.Count ?? 0;
        var instances = new Matrix4x4[1 + opponentCount];
        var tints = new Vector4[1 + opponentCount];
        instances[0] = tanks.PlayerWorld;
        tints[0] = tanks.PlayerTint;
        if (tanks.Opponents is null)
        {
            return (instances, tints);
        }

        for (var i = 0; i < tanks.Opponents.Count; i++)
        {
            instances[i + 1] = tanks.Opponents[i];
            tints[i + 1] = tanks.OpponentTints is not null && i < tanks.OpponentTints.Count
                ? tanks.OpponentTints[i]
                : Vector4.One;
        }

        return (instances, tints);
    }
}
