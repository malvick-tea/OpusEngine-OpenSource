using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>The gizmo-drag gesture of the <see cref="ViewportController"/>: pick a handle, then translate /
/// scale / rotate the selected node or light along it. The gizmo is drawn at the element's world origin and
/// the result is written back to its local transform; a translate drag also carries the rest of the
/// multi-selection through <see cref="SelectionFollowers"/>. Previews go through the document's no-undo path
/// and the drag-end commit collapses the whole gesture (primary plus followers) into one reversible edit.</summary>
public sealed partial class ViewportController
{
    private GizmoAxis _dragAxis = GizmoAxis.None;
    private GizmoMode _dragMode = GizmoMode.Translate;
    private SceneNodeId _dragNode = SceneNodeId.None;
    private EditorTransform _dragStartTransform;
    private SceneLightId _dragLight = SceneLightId.None;
    private SceneLight? _dragStartLight;
    private float _grabParameter;
    private float _grabAngle;

    // The selected node's world-minus-local position offset (its parents' contribution), captured at drag
    // start so the gizmo math runs in world space (where the node box is drawn) and writes back to the local
    // transform. Zero for a root node or a light, so an unparented drag is byte-identical.
    private Vector3 _dragWorldOffset;

    /// <summary>The gizmo axis currently being dragged, or <see cref="GizmoAxis.None"/>. The composer reads
    /// it to highlight the active handle.</summary>
    public GizmoAxis ActiveGizmoAxis => _dragAxis;

    /// <summary>Returns the gizmo axis under a viewport point (projecting the selected element's gizmo to
    /// screen — straight axis handles in move / scale mode, rings in rotate mode), or
    /// <see cref="GizmoAxis.None"/> with no selection, on a miss, or when the selected element has no gizmo
    /// in the active mode (<see cref="ElementGizmo"/>).</summary>
    public GizmoAxis PickGizmoAxis(float viewportX01, float viewportY01, EditorPanelRect viewport)
    {
        if (ElementGizmo.Origin(_document, GizmoMode) is not { } origin)
        {
            return GizmoAxis.None;
        }

        float length = TranslateGizmo.HandleLength(Camera.Distance);
        var viewProjection = Camera.ViewMatrix * Camera.ProjectionMatrix(viewport.AspectRatio);
        var click = new Vector2(viewportX01 * viewport.Width, viewportY01 * viewport.Height);
        var handles = GizmoMode == GizmoMode.Rotate
            ? ProjectRingHandles(origin, length, viewProjection, viewport)
            : ProjectAxisHandles(origin, length, viewProjection, viewport);
        return GizmoAxisPicker.Pick(click, handles);
    }

    private static List<GizmoScreenHandle> ProjectAxisHandles(
        Vector3 origin, float length, Matrix4x4 viewProjection, EditorPanelRect viewport)
    {
        var handles = new List<GizmoScreenHandle>();
        foreach (var handle in TranslateGizmo.Handles(origin, length))
        {
            if (WorldScreenProjector.TryProject(handle.Start, viewProjection, viewport.Width, viewport.Height, out var a) &&
                WorldScreenProjector.TryProject(handle.End, viewProjection, viewport.Width, viewport.Height, out var b))
            {
                handles.Add(new GizmoScreenHandle(handle.Axis, a, b));
            }
        }

        return handles;
    }

    private static List<GizmoScreenHandle> ProjectRingHandles(
        Vector3 origin, float radius, Matrix4x4 viewProjection, EditorPanelRect viewport)
    {
        var handles = new List<GizmoScreenHandle>();
        foreach (var segment in RotateGizmo.Segments(origin, radius))
        {
            if (WorldScreenProjector.TryProject(segment.A, viewProjection, viewport.Width, viewport.Height, out var a) &&
                WorldScreenProjector.TryProject(segment.B, viewProjection, viewport.Width, viewport.Height, out var b))
            {
                handles.Add(new GizmoScreenHandle(segment.Axis, a, b));
            }
        }

        return handles;
    }

    /// <summary>Begins a gizmo drag on <paramref name="axis"/>, recording the grabbed element's start state
    /// (a node's transform or a light's value) and the grab reference under the cursor — the axis parameter
    /// for move / scale, the ring angle for rotate. Returns false with no selection, when the selected
    /// element has no gizmo in the active mode, or when the pick ray is parallel to the axis (move / scale)
    /// or to the rotation plane (rotate).</summary>
    public bool BeginGizmoDrag(GizmoAxis axis, float viewportX01, float viewportY01, float aspectRatio)
    {
        if (axis == GizmoAxis.None || ElementGizmo.Origin(_document, GizmoMode) is not { } origin)
        {
            return false;
        }

        var ray = Camera.PickRay(viewportX01, viewportY01, aspectRatio);
        if (GizmoMode == GizmoMode.Rotate)
        {
            if (!RotateGizmo.TryAngle(ray, origin, axis, out _grabAngle))
            {
                return false;
            }
        }
        else if (!GizmoDragTranslation.TryResolveAxisParameter(ray, origin, TranslateGizmo.AxisUnit(axis), out _grabParameter))
        {
            return false;
        }

        _dragAxis = axis;
        _dragMode = GizmoMode;
        var element = _document.SelectedElement;
        if (element.IsLight)
        {
            _dragLight = element.AsLight;
            _dragStartLight = _document.Scene.FindLight(_dragLight);
            _dragNode = SceneNodeId.None;
            _dragWorldOffset = Vector3.Zero;
        }
        else
        {
            _dragNode = element.AsNode;
            _dragStartTransform = _document.Scene.Find(_dragNode)!.Transform;
            _dragWorldOffset = NodeWorldOffset(_dragNode, _dragStartTransform);
            _dragLight = SceneLightId.None;
            _dragStartLight = null;
        }

        // Only a translate drag moves the rest of the multi-selection with the primary; rotate and scale
        // keep their per-element pivots, so the other members stand still.
        if (_dragMode == GizmoMode.Translate)
        {
            _followers.Capture(element);
        }
        else
        {
            _followers.Clear();
        }

        return true;
    }

    /// <summary>Updates the active drag from the cursor: Translate slides the element along the axis by how
    /// far the cursor moved relative to the grab point, Scale scales the axis by the ratio of the current to
    /// the grab parameter (nodes only), and Rotate turns the element about the axis by the angle swept
    /// around the ring since the grab — a node's Euler angles, a light's aim direction. With
    /// <paramref name="snap"/> the move snaps to whole metres and the rotation to fixed-degree steps (scale
    /// is never snapped). Previews the change (no undo step) so the live mirror tracks it.</summary>
    public void UpdateGizmoDrag(float viewportX01, float viewportY01, float aspectRatio, bool snap = false)
    {
        if (_dragAxis == GizmoAxis.None)
        {
            return;
        }

        var ray = Camera.PickRay(viewportX01, viewportY01, aspectRatio);
        if (_dragStartLight is { } startLight)
        {
            if (ComputeDraggedLight(ray, startLight, snap) is { } movedLight)
            {
                if (movedLight != _document.Scene.FindLight(_dragLight))
                {
                    _document.PreviewLight(movedLight);
                }

                _followers.Preview(movedLight.Position.ToVector3() - startLight.Position.ToVector3());
            }

            return;
        }

        if (ComputeDraggedTransform(ray, snap) is not { } moved)
        {
            return;
        }

        var current = _document.Scene.Find(_dragNode);
        if (current is not null && current.Transform != moved)
        {
            _document.PreviewNodeTransform(_dragNode, moved);
        }

        _followers.Preview(moved.Position.ToVector3() - _dragStartTransform.Position.ToVector3());
    }

    private EditorTransform? ComputeDraggedTransform(Ray ray, bool snap)
    {
        // Resolve the ray against the node's world origin (the gizmo is drawn there) while the result is
        // written back to the local transform; the offset is zero for a root, so this is unchanged for an
        // unparented node.
        var origin = _dragStartTransform.Position.ToVector3() + _dragWorldOffset;
        if (_dragMode == GizmoMode.Rotate)
        {
            if (!RotateGizmo.TryAngle(ray, origin, _dragAxis, out float angle))
            {
                return null;
            }

            float deltaDegrees = RotateGizmo.DeltaDegrees(_grabAngle, angle);
            var euler = RotateGizmo.Rotate(_dragStartTransform.RotationEulerDegrees, _dragAxis, deltaDegrees);
            if (snap)
            {
                euler = GizmoSnap.SnapAxis(euler, _dragAxis, GizmoSnap.RotateStepDegrees);
            }

            return _dragStartTransform with { RotationEulerDegrees = euler };
        }

        if (!GizmoDragTranslation.TryResolveAxisParameter(ray, origin, TranslateGizmo.AxisUnit(_dragAxis), out float parameter))
        {
            return null;
        }

        if (_dragMode == GizmoMode.Scale)
        {
            if (!ScaleGizmo.TryFactor(_grabParameter, parameter, out float factor))
            {
                return null;
            }

            var scale = ScaleGizmo.Scale(_dragStartTransform.Scale, _dragAxis, factor);
            if (snap)
            {
                scale = GizmoSnap.SnapScaleAxis(scale, _dragAxis, GizmoSnap.ScaleStep);
            }

            return _dragStartTransform with { Scale = scale };
        }

        var position = TranslateGizmo.Translate(_dragStartTransform.Position, _dragAxis, parameter - _grabParameter);
        if (snap)
        {
            position = GizmoSnap.SnapAxis(position, _dragAxis, GizmoSnap.TranslateStepMeters);
        }

        return _dragStartTransform with { Position = position };
    }

    /// <summary>Ends the active drag, committing the net group change as a single reversible edit when
    /// anything actually changed. A grab with no movement leaves the document untouched.</summary>
    public void EndGizmoDrag()
    {
        if (_dragAxis == GizmoAxis.None)
        {
            return;
        }

        var nodes = new List<NodeMove>();
        var lights = new List<LightMove>();
        if (_dragStartLight is { } startLight)
        {
            var currentLight = _document.Scene.FindLight(_dragLight);
            if (currentLight is not null && currentLight != startLight)
            {
                lights.Add(new LightMove(startLight, currentLight));
            }
        }
        else if (_document.Scene.Find(_dragNode) is { } node && node.Transform != _dragStartTransform)
        {
            nodes.Add(new NodeMove(_dragNode, _dragStartTransform, node.Transform));
        }

        _followers.AppendMoves(nodes, lights);
        if (nodes.Count > 0 || lights.Count > 0)
        {
            _document.CommitGroupTransform(nodes, lights);
        }

        _dragAxis = GizmoAxis.None;
        _dragNode = SceneNodeId.None;
        _dragLight = SceneLightId.None;
        _dragStartLight = null;
        _followers.Clear();
    }

    private SceneLight? ComputeDraggedLight(Ray ray, SceneLight start, bool snap)
    {
        var origin = start.Position.ToVector3();
        if (_dragMode == GizmoMode.Rotate)
        {
            if (!RotateGizmo.TryAngle(ray, origin, _dragAxis, out float angle))
            {
                return null;
            }

            float deltaDegrees = RotateGizmo.DeltaDegrees(_grabAngle, angle);
            if (snap)
            {
                deltaDegrees = GizmoSnap.ToStep(deltaDegrees, GizmoSnap.RotateStepDegrees);
            }

            return start with { Direction = LightAim.Rotate(start.Direction, _dragAxis, deltaDegrees) };
        }

        if (!GizmoDragTranslation.TryResolveAxisParameter(ray, origin, TranslateGizmo.AxisUnit(_dragAxis), out float parameter))
        {
            return null;
        }

        var position = TranslateGizmo.Translate(start.Position, _dragAxis, parameter - _grabParameter);
        if (snap)
        {
            position = GizmoSnap.SnapAxis(position, _dragAxis, GizmoSnap.TranslateStepMeters);
        }

        return start with { Position = position };
    }
}
