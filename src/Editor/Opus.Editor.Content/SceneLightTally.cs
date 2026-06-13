namespace Opus.Editor.Content;

/// <summary>
/// A scene's light count broken down by kind — engine-neutral developer information surfaced in the scene
/// content report so a scene's lighting load is visible at a glance alongside its geometry cost.
/// </summary>
/// <param name="Directional">Directional (sun / key) light count.</param>
/// <param name="Point">Point light count.</param>
/// <param name="Spot">Spot light count.</param>
public readonly record struct SceneLightTally(int Directional, int Point, int Spot)
{
    /// <summary>Total lights across all kinds.</summary>
    public int Total => Directional + Point + Spot;
}
