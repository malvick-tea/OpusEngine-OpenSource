using System;
using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>
/// An in-progress marquee (box select) drag, in normalised viewport coordinates (0,0 top-left,
/// 1,1 bottom-right): where the Shift+press anchored and where the cursor is now. The controller holds
/// it, the composer draws the rubber-band rectangle from it, and ending the drag selects what the box
/// contains. Normalised rather than pixel coordinates so a resize mid-drag cannot skew the box.
/// </summary>
/// <param name="Start">The anchored corner — where the drag began.</param>
/// <param name="Current">The moving corner — where the cursor is now.</param>
public readonly record struct MarqueeState(Vector2 Start, Vector2 Current)
{
    /// <summary>The rectangle's top-left, whichever way the drag travelled.</summary>
    public Vector2 Min => new(MathF.Min(Start.X, Current.X), MathF.Min(Start.Y, Current.Y));

    /// <summary>The rectangle's bottom-right, whichever way the drag travelled.</summary>
    public Vector2 Max => new(MathF.Max(Start.X, Current.X), MathF.Max(Start.Y, Current.Y));
}
