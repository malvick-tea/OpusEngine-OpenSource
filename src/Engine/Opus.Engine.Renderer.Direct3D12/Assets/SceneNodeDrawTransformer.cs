using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Pure utility for re-projecting a flattened glTF node-draw list under additional
/// world transforms (and optional per-instance tint). Two flavours: <see cref="Apply"/>
/// moves a single instance of the asset, <see cref="Instantiate"/> emits one copy per
/// supplied instance — useful when several entities share the same geometry (e.g. every
/// instance in a scene draws from the same mesh set, optionally with a per-instance
/// tint).</summary>
public static class SceneNodeDrawTransformer
{
    /// <summary>Returns a new list where each entry's world matrix is post-multiplied by
    /// <paramref name="extraWorld"/>. Tint is preserved from the source draws. The original
    /// list is not mutated.</summary>
    public static List<SceneNodeDraw> Apply(IReadOnlyList<SceneNodeDraw> draws, in Matrix4x4 extraWorld)
    {
        ArgumentNullException.ThrowIfNull(draws);
        var result = new List<SceneNodeDraw>(draws.Count);
        for (var i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            result.Add(draw with { World = draw.World * extraWorld });
        }

        return result;
    }

    /// <summary>For each transform in <paramref name="instances"/>, emits one full copy of
    /// <paramref name="template"/> with every node's world multiplied by the instance
    /// transform. Output size = <c>template.Count * instances.Count</c>. Tint is preserved
    /// from the template (every instance inherits the template's per-node tint).</summary>
    public static List<SceneNodeDraw> Instantiate(
        IReadOnlyList<SceneNodeDraw> template,
        IReadOnlyList<Matrix4x4> instances)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(instances);
        var result = new List<SceneNodeDraw>(template.Count * instances.Count);
        for (var i = 0; i < instances.Count; i++)
        {
            var world = instances[i];
            for (var n = 0; n < template.Count; n++)
            {
                var draw = template[n];
                result.Add(draw with { World = draw.World * world });
            }
        }

        return result;
    }

    /// <summary>Like <see cref="Instantiate(IReadOnlyList{SceneNodeDraw}, IReadOnlyList{Matrix4x4})"/>
    /// but each instance also multiplies the template's per-node tint by an instance tint
    /// — supports per-school camo / per-team palette swap without uploading new albedos.
    /// Counts must match (<paramref name="instances"/>.Count == <paramref name="instanceTints"/>.Count);
    /// pass <c>Vector4.One</c> for the identity-tint instance.</summary>
    public static List<SceneNodeDraw> Instantiate(
        IReadOnlyList<SceneNodeDraw> template,
        IReadOnlyList<Matrix4x4> instances,
        IReadOnlyList<Vector4> instanceTints)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(instanceTints);
        if (instances.Count != instanceTints.Count)
        {
            throw new ArgumentException(
                $"instances.Count ({instances.Count}) must match instanceTints.Count ({instanceTints.Count}).",
                nameof(instanceTints));
        }

        var result = new List<SceneNodeDraw>(template.Count * instances.Count);
        for (var i = 0; i < instances.Count; i++)
        {
            var world = instances[i];
            var instanceTint = instanceTints[i];
            for (var n = 0; n < template.Count; n++)
            {
                var draw = template[n];
                result.Add(draw with { World = draw.World * world, TintFactor = draw.TintFactor * instanceTint });
            }
        }

        return result;
    }
}
