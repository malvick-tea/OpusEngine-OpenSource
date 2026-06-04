using System.Collections.Generic;
using System.Numerics;

namespace Opus.Engine.Ui;

/// <summary>One freehand ink stroke: an ordered polyline of surface-pixel points drawn at a
/// uniform pixel width and colour. Immutable — the in-progress stroke in
/// <see cref="InkAnnotationLayer"/> is materialised into one of these on commit. A single-point
/// stroke renders as a dot (one round cap); an empty stroke renders nothing. Coordinates are
/// surface pixels (top-left origin, y down), matching every other <see cref="IDrawSurface"/>
/// primitive — the host maps pointer input into this space.</summary>
public sealed record InkStroke(IReadOnlyList<Vector2> Points, float WidthPx, Color Color);
