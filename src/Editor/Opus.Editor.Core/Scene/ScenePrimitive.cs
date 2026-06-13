using System;

namespace Opus.Editor.Core;

/// <summary>The built-in primitive shapes a scene node can carry without any content package.</summary>
public enum ScenePrimitiveKind
{
    /// <summary>A unit cube (1 m edges, centred on the origin).</summary>
    Cube,

    /// <summary>A sphere of 0.5 m radius, centred on the origin.</summary>
    Sphere,

    /// <summary>A cylinder of 0.5 m radius and 1 m height along Y, centred on the origin.</summary>
    Cylinder,

    /// <summary>A 1 x 1 m flat quad on the Y=0 plane, centred on the origin.</summary>
    Plane,

    /// <summary>A cone of 0.5 m base radius and 1 m height along Y, centred on the origin.</summary>
    Cone,
}

/// <summary>
/// The asset-reference scheme for built-in primitives: a node whose <see cref="SceneNode.AssetRef"/> is
/// <c>primitive:cube</c> (or sphere / cylinder / plane / cone) is an authorable object that needs no
/// content package — the editor draws its true shape and the consumer maps it to real geometry. Kept in
/// the document core (not the UI) because the reference is part of the persisted scene format, exactly
/// like a model path.
/// </summary>
public static class ScenePrimitive
{
    /// <summary>The scheme prefix that marks an asset reference as a built-in primitive.</summary>
    public const string SchemePrefix = "primitive:";

    /// <summary>The canonical asset reference for a primitive kind (for example <c>primitive:cube</c>).</summary>
    public static string AssetRef(ScenePrimitiveKind kind) => SchemePrefix + DefaultName(kind);

    /// <summary>The lower-case shape name used both in the asset reference and as the stem of a freshly
    /// placed node's display name.</summary>
    public static string DefaultName(ScenePrimitiveKind kind) => kind switch
    {
        ScenePrimitiveKind.Sphere => "sphere",
        ScenePrimitiveKind.Cylinder => "cylinder",
        ScenePrimitiveKind.Plane => "plane",
        ScenePrimitiveKind.Cone => "cone",
        _ => "cube",
    };

    /// <summary>Parses an asset reference back to its primitive kind, or null when the reference is null,
    /// not in the primitive scheme, or names an unknown shape. Case-insensitive so a hand-edited scene
    /// file stays valid regardless of casing.</summary>
    public static ScenePrimitiveKind? TryParse(string? assetRef)
    {
        if (assetRef is null || !assetRef.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = assetRef.AsSpan(SchemePrefix.Length);
        foreach (var kind in Kinds)
        {
            if (name.Equals(DefaultName(kind), StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>Every primitive kind, in toolbar / shortcut order.</summary>
    public static ReadOnlySpan<ScenePrimitiveKind> Kinds => new[]
    {
        ScenePrimitiveKind.Cube,
        ScenePrimitiveKind.Sphere,
        ScenePrimitiveKind.Cylinder,
        ScenePrimitiveKind.Plane,
        ScenePrimitiveKind.Cone,
    };
}
