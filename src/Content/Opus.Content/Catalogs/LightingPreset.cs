using System.Numerics;

namespace Opus.Content;

/// <summary>
/// Loaded lighting preset for a scene — four scene-tuned colours / vectors that the
/// renderer composes into its abstract <c>DirectionalLight</c> + <c>SkySetup</c>
/// types. <see cref="SunDirection"/> is the unnormalised direction read from the data
/// file (loader normalises before storing); the other three are linear-space RGB
/// triplets multiplied by per-channel scene exposure during the forward pass.
/// </summary>
/// <remarks>
/// Lives in <c>Content</c> because lighting is scene-tuning data: an artist tweaks the
/// values in <c>data/*.csv</c> without recompiling the renderer. A scene loads its preset
/// CSV (e.g. <c>data/garage-lighting.csv</c>); separate scenes ship separate CSVs keyed by
/// location and time of day (clear noon, overcast snow, desert dusk, etc.).
/// </remarks>
public readonly record struct LightingPreset(
    Vector3 SunDirection,
    Vector3 SunColour,
    Vector3 AmbientColour,
    Vector3 HorizonColour);
