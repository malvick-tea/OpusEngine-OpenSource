using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>The handle-free gestures of the <see cref="ViewportController"/>: a direct planar drag (press on
/// the selected element and slide it across its own horizontal plane) and a marquee (Shift+drag box select).
/// The planar drag carries the multi-selection through <see cref="SelectionFollowers"/> and commits as one
/// reversible edit, exactly like a gizmo drag; the marquee resolves its rectangle through
/// <see cref="MarqueeSelect"/>.</summary>
public sealed partial class ViewportController
{
    private bool _planarDrag;
    private SceneNodeId _planarNode = SceneNodeId.None;
    private EditorTransform _planarStartTransform;
    private SceneLightId _planarLight = SceneLightId.None;
    private SceneLight? _planarStartLight;
    private Vector3 _planarGrabOffset;
    private Vector3 _planarWorldOffset;
    private float _planarPlaneY;

    /// <summary>Begins a direct move of the selected element when the press lands on it: the grab point is
    /// where the pick ray meets the element's horizontal plane, and the drag slides the element across that
    /// plane (Y stays fixed) — no gizmo handle needed. False when nothing is selected, the press misses the
    /// selected element (so the gesture falls through to an orbit / selection click), or the view is
    /// edge-on to the plane.</summary>
    public bool BeginPlanarDrag(float viewportX01, float viewportY01, float aspectRatio)
    {
        var element = _document.SelectedElement;
        if (!element.IsValid)
        {
            return false;
        }

        var ray = Camera.PickRay(viewportX01, viewportY01, aspectRatio);
        var picked = ViewportPicker.PickElement(ray, ScenePickList.BuildElements(_document.Scene, _bounds));
        if (!picked.Hit || picked.Element != element)
        {
            return false;
        }

        Vector3 origin;
        if (element.IsNode && _document.Scene.Find(element.AsNode) is { } node)
        {
            _planarNode = node.Id;
            _planarStartTransform = node.Transform;
            _planarWorldOffset = NodeWorldOffset(node.Id, node.Transform);
            origin = node.Transform.Position.ToVector3() + _planarWorldOffset;
            _planarLight = SceneLightId.None;
            _planarStartLight = null;
        }
        else if (element.IsLight && _document.Scene.FindLight(element.AsLight) is { } light)
        {
            origin = light.Position.ToVector3();
            _planarWorldOffset = Vector3.Zero;
            _planarLight = light.Id;
            _planarStartLight = light;
            _planarNode = SceneNodeId.None;
        }
        else
        {
            return false;
        }

        if (!TryHitHorizontalPlane(ray, origin.Y, out var grabPoint))
        {
            _planarNode = SceneNodeId.None;
            _planarLight = SceneLightId.None;
            _planarStartLight = null;
            return false;
        }

        _planarPlaneY = origin.Y;
        _planarGrabOffset = grabPoint - origin;
        _planarDrag = true;
        _followers.Capture(element);
        return true;
    }

    /// <summary>Updates the active planar drag from the cursor: the element follows the pick ray's hit on
    /// the grab plane, offset so it does not jump to the cursor. With <paramref name="snap"/> the X / Z
    /// land on whole metres. Previews the change (no undo step), exactly like a gizmo drag frame.</summary>
    public void UpdatePlanarDrag(float viewportX01, float viewportY01, float aspectRatio, bool snap = false)
    {
        if (!_planarDrag)
        {
            return;
        }

        var ray = Camera.PickRay(viewportX01, viewportY01, aspectRatio);
        if (!TryHitHorizontalPlane(ray, _planarPlaneY, out var hit))
        {
            return;
        }

        var position = hit - _planarGrabOffset;
        if (snap)
        {
            position = new Vector3(MathF.Round(position.X), position.Y, MathF.Round(position.Z));
        }

        if (_planarStartLight is { } startLight)
        {
            var moved = startLight with { Position = Float3.FromVector3(position with { Y = startLight.Position.Y }) };
            if (moved != _document.Scene.FindLight(_planarLight))
            {
                _document.PreviewLight(moved);
            }

            _followers.Preview(moved.Position.ToVector3() - startLight.Position.ToVector3());
            return;
        }

        // The plane math runs in world space; convert back to the node's local frame (subtract its parents'
        // offset) before writing. The offset is zero for a root, so this is unchanged for an unparented node.
        var localPosition = position - _planarWorldOffset;
        var transform = _planarStartTransform with
        {
            Position = Float3.FromVector3(localPosition with { Y = _planarStartTransform.Position.Y }),
        };
        var current = _document.Scene.Find(_planarNode);
        if (current is not null && current.Transform != transform)
        {
            _document.PreviewNodeTransform(_planarNode, transform);
        }

        _followers.Preview(transform.Position.ToVector3() - _planarStartTransform.Position.ToVector3());
    }

    /// <summary>Ends the planar drag, committing the net group move as a single reversible edit when
    /// anything actually moved — a grab with no movement leaves the document and history untouched.</summary>
    public void EndPlanarDrag()
    {
        if (!_planarDrag)
        {
            return;
        }

        var nodes = new List<NodeMove>();
        var lights = new List<LightMove>();
        if (_planarStartLight is { } startLight)
        {
            var current = _document.Scene.FindLight(_planarLight);
            if (current is not null && current != startLight)
            {
                lights.Add(new LightMove(startLight, current));
            }
        }
        else if (_document.Scene.Find(_planarNode) is { } node && node.Transform != _planarStartTransform)
        {
            nodes.Add(new NodeMove(_planarNode, _planarStartTransform, node.Transform));
        }

        _followers.AppendMoves(nodes, lights);
        if (nodes.Count > 0 || lights.Count > 0)
        {
            _document.CommitGroupTransform(nodes, lights);
        }

        _planarDrag = false;
        _planarNode = SceneNodeId.None;
        _planarLight = SceneLightId.None;
        _planarStartLight = null;
        _followers.Clear();
    }

    /// <summary>Intersects a pick ray with the horizontal plane at <paramref name="planeY"/>; false when
    /// the ray is parallel to it or the hit lies behind the eye.</summary>
    private static bool TryHitHorizontalPlane(Ray ray, float planeY, out Vector3 hit)
    {
        hit = default;
        if (MathF.Abs(ray.Direction.Y) < 1e-6f)
        {
            return false;
        }

        float t = (planeY - ray.Origin.Y) / ray.Direction.Y;
        if (t <= 0f)
        {
            return false;
        }

        hit = ray.Origin + (ray.Direction * t);
        return true;
    }

    /// <summary>The in-progress marquee (box select) drag, or null. The composer draws the rubber band
    /// from it; the input mapper drives it with Shift+drag.</summary>
    public MarqueeState? Marquee { get; private set; }

    /// <summary>Anchors a marquee at a normalised viewport point (the Shift+press).</summary>
    public void BeginMarquee(float viewportX01, float viewportY01) =>
        Marquee = new MarqueeState(
            new Vector2(viewportX01, viewportY01), new Vector2(viewportX01, viewportY01));

    /// <summary>Tracks the marquee's moving corner to the cursor.</summary>
    public void UpdateMarquee(float viewportX01, float viewportY01)
    {
        if (Marquee is { } marquee)
        {
            Marquee = marquee with { Current = new Vector2(viewportX01, viewportY01) };
        }
    }

    /// <summary>Ends the marquee, selecting every visible element whose anchor the box contains
    /// (<see cref="MarqueeSelect"/>). A plain box replaces the selection — boxing empty space clears it —
    /// and an <paramref name="additive"/> box (Ctrl held) unions into it, so a sloppy additive box never
    /// throws away a built-up selection. No-op without an active marquee.</summary>
    public void EndMarquee(EditorPanelRect viewport, bool additive = false)
    {
        if (Marquee is not { } marquee)
        {
            return;
        }

        Marquee = null;
        var viewProjection = Camera.ViewMatrix * Camera.ProjectionMatrix(viewport.AspectRatio);
        var min = new Vector2(marquee.Min.X * viewport.Width, marquee.Min.Y * viewport.Height);
        var max = new Vector2(marquee.Max.X * viewport.Width, marquee.Max.Y * viewport.Height);
        var inside = MarqueeSelect.Collect(
            _document.Scene, viewProjection, viewport.Width, viewport.Height, min, max);
        _document.SelectElements(inside, additive);
    }

    /// <summary>Abandons the marquee without touching the selection (the drag never travelled past the
    /// click threshold, so the gesture resolved as a click instead).</summary>
    public void CancelMarquee() => Marquee = null;
}
