using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Engine.Ui;

/// <summary>
/// A surface's persistent freehand annotations: the marks already committed, the stroke currently
/// being drawn, and per-stroke undo / redo. The consumer feeds pointer samples
/// (<see cref="BeginStroke"/> → <see cref="AddPoint"/>* → <see cref="EndStroke"/>); the engine owns
/// the stroke geometry and history, so a commander-style hand-drawn map needs no engine workaround.
/// Pure and GPU-free — render it through a backend draw surface
/// (<c>D3D12DrawSurface.DrawAnnotations</c>).
/// </summary>
/// <remarks>
/// Genre-neutral: this models ink and its history, not what a mark means. <see cref="AddPoint"/>
/// drops samples closer than the configured minimum so a fast drag does not flood the stroke with
/// near-coincident points. Undo / redo operate per committed stroke; <see cref="Clear"/> wipes
/// every committed mark and is terminal (not itself undoable in this version).
/// </remarks>
public sealed class InkAnnotationLayer
{
    /// <summary>Default minimum distance (surface pixels) between accepted points, so a fast drag
    /// does not flood the stroke with near-coincident samples.</summary>
    public const float DefaultMinPointDistancePx = 2f;

    private readonly List<InkStroke> _committed = new();
    private readonly List<InkStroke> _redo = new();
    private readonly List<Vector2> _activePoints = new();
    private readonly float _minPointDistanceSquared;
    private float _activeWidthPx;
    private Color _activeColor;
    private bool _drawing;

    public InkAnnotationLayer(float minPointDistancePx = DefaultMinPointDistancePx)
    {
        if (minPointDistancePx < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minPointDistancePx), minPointDistancePx, "Minimum point distance must be non-negative.");
        }

        _minPointDistanceSquared = minPointDistancePx * minPointDistancePx;
    }

    /// <summary>The committed marks, oldest first — the strokes a redraw should render under the
    /// in-progress preview.</summary>
    public IReadOnlyList<InkStroke> CommittedStrokes => _committed;

    /// <summary>True between <see cref="BeginStroke"/> and <see cref="EndStroke"/> /
    /// <see cref="CancelStroke"/>.</summary>
    public bool IsDrawing => _drawing;

    public bool CanUndo => _committed.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>A snapshot of the in-progress stroke for live preview, or null when no stroke is
    /// being drawn or it has no points yet. Allocates a fresh immutable copy so the preview is
    /// stable while the live point buffer keeps growing.</summary>
    public InkStroke? InProgressStroke =>
        _drawing && _activePoints.Count > 0
            ? new InkStroke(_activePoints.ToArray(), _activeWidthPx, _activeColor)
            : null;

    /// <summary>Starts a new stroke at the given pixel width and colour, discarding any
    /// uncommitted in-progress points.</summary>
    public void BeginStroke(float widthPx, Color color)
    {
        if (widthPx <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPx), widthPx, "Stroke width must be positive.");
        }

        _drawing = true;
        _activeWidthPx = widthPx;
        _activeColor = color;
        _activePoints.Clear();
    }

    /// <summary>Adds a point to the in-progress stroke, dropping it when it is closer than the
    /// configured minimum to the last accepted point. No-op when not drawing.</summary>
    public void AddPoint(Vector2 point)
    {
        if (!_drawing)
        {
            return;
        }

        if (_activePoints.Count > 0 &&
            Vector2.DistanceSquared(_activePoints[^1], point) < _minPointDistanceSquared)
        {
            return;
        }

        _activePoints.Add(point);
    }

    /// <summary>Commits the in-progress stroke (when it has at least one point) as a new mark and
    /// clears the redo history. Returns true when a stroke was committed; a begin with no accepted
    /// points commits nothing. No-op when not drawing.</summary>
    public bool EndStroke()
    {
        if (!_drawing)
        {
            return false;
        }

        _drawing = false;
        if (_activePoints.Count == 0)
        {
            return false;
        }

        _committed.Add(new InkStroke(_activePoints.ToArray(), _activeWidthPx, _activeColor));
        _activePoints.Clear();
        _redo.Clear();
        return true;
    }

    /// <summary>Abandons the in-progress stroke without committing it. No-op when not drawing.</summary>
    public void CancelStroke()
    {
        _drawing = false;
        _activePoints.Clear();
    }

    /// <summary>Removes the most recent committed stroke and pushes it onto the redo stack. Returns
    /// false when there is nothing to undo.</summary>
    public bool Undo()
    {
        if (_committed.Count == 0)
        {
            return false;
        }

        var last = _committed[^1];
        _committed.RemoveAt(_committed.Count - 1);
        _redo.Add(last);
        return true;
    }

    /// <summary>Re-commits the most recently undone stroke. Returns false when there is nothing to
    /// redo. The redo stack is cleared whenever a new stroke is committed.</summary>
    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var stroke = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _committed.Add(stroke);
        return true;
    }

    /// <summary>Removes every committed mark and resets the redo history. Does not touch an
    /// in-progress stroke.</summary>
    public void Clear()
    {
        _committed.Clear();
        _redo.Clear();
    }
}
