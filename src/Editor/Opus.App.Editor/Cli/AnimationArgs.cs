namespace Opus.App.Editor.Cli;

/// <summary>
/// Animation-specific CLI parameters, kept off <see cref="EditorArgs"/> so the common shape stays small.
/// Populated only for the <c>anim-state</c> and <c>anim-transition</c> commands; the graph path and
/// display name reuse <see cref="EditorArgs.ScenePath"/> / <see cref="EditorArgs.SceneName"/>.
/// </summary>
/// <param name="StateName">State name to add (anim-state).</param>
/// <param name="Clip">Clip asset reference to bind to the new state, or null.</param>
/// <param name="Loop">Whether the new state's clip loops.</param>
/// <param name="Speed">Playback-speed multiplier for the new state.</param>
/// <param name="MakeEntry">When true, the new state also becomes the graph's entry state.</param>
/// <param name="FromState">Source state name (anim-transition).</param>
/// <param name="ToState">Destination state name (anim-transition).</param>
/// <param name="Trigger">Trigger name that fires the transition (anim-transition).</param>
/// <param name="Blend">Cross-fade duration in seconds (anim-transition).</param>
public sealed record AnimationArgs(
    string? StateName = null,
    string? Clip = null,
    bool Loop = true,
    float Speed = 1f,
    bool MakeEntry = false,
    string? FromState = null,
    string? ToState = null,
    string? Trigger = null,
    float Blend = 0f);
