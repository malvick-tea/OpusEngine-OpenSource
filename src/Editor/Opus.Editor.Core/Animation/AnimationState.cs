namespace Opus.Editor.Core;

/// <summary>
/// One state in an animation state graph: an identity, a display name, the clip it plays (an asset
/// reference — null for an empty placeholder state authored before its clip exists), whether that clip
/// loops, and a playback-speed multiplier. Engine-neutral orchestration data — the graph wires generic
/// states and transitions; the actual clip samples are consumer content the runtime binds by reference.
/// Immutable — edits go through commands that replace the state, so undo / redo and the pseudo-code mirror
/// always observe a consistent snapshot.
/// </summary>
/// <param name="Id">Stable identity within the graph.</param>
/// <param name="Name">Human-readable state name (unique within a valid graph).</param>
/// <param name="ClipRef">Clip asset reference this state plays, or null for an unbound state.</param>
/// <param name="Loop">Whether the bound clip loops while the state is active.</param>
/// <param name="Speed">Playback-speed multiplier (1 = clip-authored speed).</param>
public sealed record AnimationState(AnimationStateId Id, string Name, string? ClipRef, bool Loop, float Speed)
{
    /// <summary>The default playback speed — the clip's authored rate.</summary>
    public const float DefaultSpeed = 1f;

    public AnimationState WithName(string name) => this with { Name = name };

    public AnimationState WithClip(string? clipRef) => this with { ClipRef = clipRef };

    public AnimationState WithLoop(bool loop) => this with { Loop = loop };

    public AnimationState WithSpeed(float speed) => this with { Speed = speed };
}
