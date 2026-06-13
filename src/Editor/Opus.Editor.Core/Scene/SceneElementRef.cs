namespace Opus.Editor.Core;

/// <summary>The element kinds an editor scene holds.</summary>
public enum SceneElementKind
{
    /// <summary>A placed node.</summary>
    Node,

    /// <summary>A scene light.</summary>
    Light,
}

/// <summary>
/// A kind-discriminated reference to one scene element — a node or a light — so selection, outliner rows,
/// and viewport picking can speak about "the element" without growing parallel APIs per kind. The id value
/// is the element's dense id within its own kind's sequence; an id of zero is the "nothing" sentinel
/// (<see cref="None"/>), matching the id types' own conventions.
/// </summary>
/// <param name="Kind">Which element kind the id addresses.</param>
/// <param name="Id">The element's dense id value within its kind; zero selects nothing.</param>
public readonly record struct SceneElementRef(SceneElementKind Kind, int Id)
{
    /// <summary>The empty reference: selects nothing of either kind.</summary>
    public static readonly SceneElementRef None = new(SceneElementKind.Node, 0);

    public bool IsValid => Id > 0;

    /// <summary>True when this references a node (and is not <see cref="None"/>).</summary>
    public bool IsNode => IsValid && Kind == SceneElementKind.Node;

    /// <summary>True when this references a light (and is not <see cref="None"/>).</summary>
    public bool IsLight => IsValid && Kind == SceneElementKind.Light;

    /// <summary>The reference as a node id — <see cref="SceneNodeId.None"/> when this is not a node.</summary>
    public SceneNodeId AsNode => IsNode ? new SceneNodeId(Id) : SceneNodeId.None;

    /// <summary>The reference as a light id — <see cref="SceneLightId.None"/> when this is not a light.</summary>
    public SceneLightId AsLight => IsLight ? new SceneLightId(Id) : SceneLightId.None;

    public static SceneElementRef Node(SceneNodeId id) => new(SceneElementKind.Node, id.Value);

    public static SceneElementRef Light(SceneLightId id) => new(SceneElementKind.Light, id.Value);
}
