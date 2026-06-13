namespace Opus.Editor.Core;

/// <summary>
/// Common contract for the editor's dense element ids (node, light). The
/// <see cref="SceneElementStore{TId,TElement}"/> reads <see cref="Value"/> to compute the next id after a
/// load, so a store can be generic over any element id without knowing its concrete type. Internal: the id
/// types are public, but this shared seam is an implementation detail of the store.
/// </summary>
internal interface ISceneElementId
{
    /// <summary>The 1-based dense id value; zero is the reserved "none" sentinel.</summary>
    int Value { get; }
}
