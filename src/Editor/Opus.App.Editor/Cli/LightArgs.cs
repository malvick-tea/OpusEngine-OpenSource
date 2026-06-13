using Opus.Editor.Core;

namespace Opus.App.Editor.Cli;

/// <summary>
/// Light-specific CLI parameters, kept off <see cref="EditorArgs"/> so the common shape stays small.
/// Populated only for the <c>light-add</c> command; the scene path and the light position reuse
/// <see cref="EditorArgs.ScenePath"/> / <see cref="EditorArgs.Position"/>, and the display name reuses
/// <see cref="EditorArgs.SceneName"/>. Every override is nullable: a null leaves the per-kind default the
/// <see cref="SceneLight"/> creators seed.
/// </summary>
/// <param name="Kind">Light model to create (light-add). Null for light-edit, which keeps the existing
/// light's kind.</param>
/// <param name="Color">Linear RGB colour override, or null for the default white.</param>
/// <param name="Intensity">Intensity override, or null for the default.</param>
/// <param name="Direction">Aim direction override (directional / spot), or null for the default down.</param>
/// <param name="Range">Attenuation range override in metres (point / spot), or null for the default.</param>
/// <param name="ConeInnerAngleDegrees">Spot inner cone half-angle override, or null for the default.</param>
/// <param name="ConeOuterAngleDegrees">Spot outer cone half-angle override, or null for the default.</param>
public sealed record LightArgs(
    SceneLightKind? Kind = null,
    Float3? Color = null,
    float? Intensity = null,
    Float3? Direction = null,
    float? Range = null,
    float? ConeInnerAngleDegrees = null,
    float? ConeOuterAngleDegrees = null);
