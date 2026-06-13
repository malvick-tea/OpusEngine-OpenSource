namespace Opus.Editor.Core;

/// <summary>
/// One light in an editor scene: identity, display name, lighting model, and the parameters that model
/// needs — colour, intensity, and per-kind position / direction / range / cone. Engine-neutral generic
/// lighting data (no game rule), so it lives in the editor / engine tier. Immutable: the scene's light
/// commands replace it, so undo / redo and the pseudo-code mirror always observe a consistent snapshot.
/// <para>
/// Not every field is meaningful for every <see cref="SceneLightKind"/> — a directional light uses only
/// <see cref="Direction"/>; a point light uses <see cref="Position"/> and <see cref="Range"/>; a spot light
/// uses position, direction, range, and the cone angles. The pseudo-code mirror prints only the fields the
/// kind uses, but every field is carried so changing a light's kind never loses authored values.
/// </para>
/// </summary>
/// <param name="Id">Stable identity within the document.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="Kind">Lighting model.</param>
/// <param name="Color">Linear RGB colour (components nominally 0..1).</param>
/// <param name="Intensity">Brightness in the renderer's light-unit scale.</param>
/// <param name="Position">World position (point / spot lights).</param>
/// <param name="Direction">Aim direction (directional / spot lights); not required to be normalised.</param>
/// <param name="Range">Attenuation range in metres (point / spot lights).</param>
/// <param name="SpotInnerAngleDegrees">Spot inner cone half-angle in degrees — the fully-lit core.</param>
/// <param name="SpotOuterAngleDegrees">Spot outer cone half-angle in degrees — where the cone fades to dark.</param>
public sealed record SceneLight(
    SceneLightId Id,
    string Name,
    SceneLightKind Kind,
    Float3 Color,
    float Intensity,
    Float3 Position,
    Float3 Direction,
    float Range,
    float SpotInnerAngleDegrees,
    float SpotOuterAngleDegrees)
{
    /// <summary>Default white light colour (linear RGB).</summary>
    public static readonly Float3 DefaultColor = Float3.One;

    /// <summary>Down (-Y): the natural default aim for a sun or spot light.</summary>
    public static readonly Float3 DefaultDirection = new(0f, -1f, 0f);

    /// <summary>Default intensity in the renderer's light-unit scale.</summary>
    public const float DefaultIntensity = 1f;

    /// <summary>Default attenuation range for positioned lights, in metres.</summary>
    public const float DefaultRangeMeters = 10f;

    /// <summary>Default spot inner cone half-angle, in degrees.</summary>
    public const float DefaultSpotInnerAngleDegrees = 20f;

    /// <summary>Default spot outer cone half-angle, in degrees.</summary>
    public const float DefaultSpotOuterAngleDegrees = 30f;

    /// <summary>A directional (sun / key) light aimed straight down with default colour and intensity and an
    /// unallocated id — <see cref="EditorScene"/> assigns the id when the light is added.</summary>
    public static SceneLight CreateDirectional(string name) => new(
        SceneLightId.None, name, SceneLightKind.Directional, DefaultColor, DefaultIntensity,
        Float3.Zero, DefaultDirection, 0f, 0f, 0f);

    /// <summary>An omnidirectional point light at the origin with the default range and an unallocated id.</summary>
    public static SceneLight CreatePoint(string name) => new(
        SceneLightId.None, name, SceneLightKind.Point, DefaultColor, DefaultIntensity,
        Float3.Zero, DefaultDirection, DefaultRangeMeters, 0f, 0f);

    /// <summary>A spot light at the origin aimed down with the default range / cone and an unallocated id.</summary>
    public static SceneLight CreateSpot(string name) => new(
        SceneLightId.None, name, SceneLightKind.Spot, DefaultColor, DefaultIntensity,
        Float3.Zero, DefaultDirection, DefaultRangeMeters,
        DefaultSpotInnerAngleDegrees, DefaultSpotOuterAngleDegrees);

    /// <summary>True when the light is hidden in the editor viewport (its glyph is not drawn and not
    /// click-pickable; still listed in the outliner and the mirror). An init property rather than a
    /// constructor parameter so a pre-visibility scene file still loads with no schema bump.</summary>
    public bool Hidden { get; init; }

    public SceneLight WithId(SceneLightId id) => this with { Id = id };

    public SceneLight WithName(string name) => this with { Name = name };

    public SceneLight WithHidden(bool hidden) => this with { Hidden = hidden };
}
