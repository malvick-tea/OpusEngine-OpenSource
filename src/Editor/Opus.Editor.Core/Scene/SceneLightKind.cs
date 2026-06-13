namespace Opus.Editor.Core;

/// <summary>
/// The lighting model of a <see cref="SceneLight"/>. These are the generic real-time light archetypes every
/// renderer understands — engine-neutral, not a game-specific lighting rule.
/// </summary>
public enum SceneLightKind
{
    /// <summary>A parallel light with a direction but no position or distance falloff (a sun / key light).</summary>
    Directional,

    /// <summary>An omnidirectional light at a world position, attenuated over <see cref="SceneLight.Range"/>.</summary>
    Point,

    /// <summary>A cone light with a position, direction, range, and inner / outer cone angles.</summary>
    Spot,
}
