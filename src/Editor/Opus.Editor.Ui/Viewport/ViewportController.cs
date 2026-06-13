using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>
/// The viewport "brain": owns the orbit camera and turns viewport input into edits on the document — what is
/// selected, where the camera looks, and the transform / planar / marquee gestures. Pure and GPU-free — the
/// host renders the scene and feeds it pointer input; this decides what happens. The model bounds source is
/// supplied by the host (backed by model inspection).
///
/// This is one coordinator with a stable public surface that the input mapper, frame composer, and inspector
/// input all bind to, so it stays a single type — fragmenting it into N injected objects would spread the
/// shared camera / document / bounds dependency rather than reduce it. It is split across partial files by
/// gesture / concern (editing, modal text entry, scene browser, gizmo drag, planar drag) to stay within the
/// file cap, and the genuinely independent pieces are extracted as their own types
/// (<see cref="SelectionFollowers"/>, <see cref="MarqueeSelect"/>, the gizmo math helpers).
/// </summary>
public sealed partial class ViewportController
{
    /// <summary>The longest element name the rename buffer accepts.</summary>
    public const int MaxNameLength = 64;

    private readonly EditorDocument _document;
    private readonly IModelBoundsSource _bounds;
    private readonly SelectionFollowers _followers;

    public ViewportController(EditorDocument document, IModelBoundsSource bounds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bounds);
        _document = document;
        _bounds = bounds;
        _followers = new SelectionFollowers(document);
    }

    public OrbitCamera Camera { get; } = new();

    /// <summary>Which transform channel the gizmo edits (move vs scale vs rotate). The composer reads it to
    /// draw the matching gizmo; the window switches it with the W / E / R keys.</summary>
    public GizmoMode GizmoMode { get; private set; } = GizmoMode.Translate;

    /// <summary>The document state the action toolbar reads to enable / disable its buttons. Delete and
    /// frame work on the selected element of either kind, so a selected light enables them too.</summary>
    public EditorToolbarState ToolbarState =>
        new(_document.CanUndo, _document.CanRedo, _document.SelectedElement.IsValid, _document.IsDirty);

    /// <summary>The document's scene, for the outliner to list its nodes.</summary>
    public EditorScene Scene => _document.Scene;

    /// <summary>The current node selection, for the gizmo paths that only operate on nodes.</summary>
    public SceneNodeId Selection => _document.Selection;

    /// <summary>The selected element of either kind, for the outliner to highlight its row.</summary>
    public SceneElementRef SelectedElement => _document.SelectedElement;

    /// <summary>Every selected element in selection order (the last is the primary) — what the composer
    /// highlights across the viewport and the outliner.</summary>
    public IReadOnlyList<SceneElementRef> SelectedElements => _document.SelectedElements;

    /// <summary>Selects a node by id (the outliner's click action).</summary>
    public void SelectNode(SceneNodeId id) => _document.Select(id);

    /// <summary>Selects an element of either kind — the outliner rows and the viewport pick route through
    /// here so node and light selection behave identically.</summary>
    public void SelectElement(SceneElementRef element)
    {
        if (element.IsLight)
        {
            _document.SelectLight(element.AsLight);
        }
        else
        {
            _document.Select(element.AsNode);
        }
    }

    /// <summary>Toggles one element's selection-set membership — the Ctrl+click action in the viewport
    /// and on outliner rows (<see cref="EditorDocument.ToggleSelect"/>).</summary>
    public void ToggleElement(SceneElementRef element) => _document.ToggleSelect(element);

    /// <summary>Replaces (or with <paramref name="additive"/> unions into) the selection set — the
    /// outliner's Shift+click range select routes through here
    /// (<see cref="EditorDocument.SelectElements"/>).</summary>
    public void SelectElements(IReadOnlyList<SceneElementRef> elements, bool additive = false) =>
        _document.SelectElements(elements, additive);

    /// <summary>Selects every visible element — nodes and lights — replacing the selection (Ctrl+A).
    /// Hidden elements stay out, the box-select visibility rule; false when nothing is visible.</summary>
    public bool SelectAllVisible()
    {
        var visible = new List<SceneElementRef>();
        foreach (var node in _document.Scene.Nodes)
        {
            if (!node.Hidden)
            {
                visible.Add(SceneElementRef.Node(node.Id));
            }
        }

        foreach (var light in _document.Scene.Lights)
        {
            if (!light.Hidden)
            {
                visible.Add(SceneElementRef.Light(light.Id));
            }
        }

        if (visible.Count == 0)
        {
            return false;
        }

        _document.SelectElements(visible);
        return true;
    }

    /// <summary>Picks the element — node or light — under the viewport point. A plain pick sets it as the
    /// document selection (clearing the selection on a miss); an <paramref name="additive"/> pick (Ctrl
    /// held) toggles the hit element's set membership instead, and a miss leaves the set alone so a sloppy
    /// Ctrl+click never throws away a built-up selection. Returns the pick result.</summary>
    public ElementPickResult PickAt(float viewportX01, float viewportY01, float aspectRatio, bool additive = false)
    {
        var ray = Camera.PickRay(viewportX01, viewportY01, aspectRatio);
        var candidates = ScenePickList.BuildElements(_document.Scene, _bounds);
        var result = ViewportPicker.PickElement(ray, candidates);
        if (additive)
        {
            if (result.Hit)
            {
                _document.ToggleSelect(result.Element);
            }
        }
        else
        {
            SelectElement(result.Hit ? result.Element : SceneElementRef.None);
        }

        return result;
    }

    /// <summary>How many rows the outliner is scrolled past the top. The composer renders from here and the
    /// outliner input mapper drives it with the wheel; never negative.</summary>
    public int OutlinerScroll { get; private set; }

    /// <summary>Sets the outliner scroll offset, floored at zero. The upper bound (so the last row stays in
    /// view) is enforced by the input mapper, which knows the panel's page size.</summary>
    public void SetOutlinerScroll(int rows) => OutlinerScroll = Math.Max(0, rows);

    /// <summary>How many lines the pseudo-code mirror is scrolled past the top. The composer slices the
    /// mirror text from here and the mirror input mapper drives it with the wheel; never negative.</summary>
    public int MirrorScroll { get; private set; }

    /// <summary>Sets the mirror scroll offset, floored at zero — the upper bound is the input mapper's,
    /// exactly like the outliner scroll.</summary>
    public void SetMirrorScroll(int lines) => MirrorScroll = Math.Max(0, lines);

    /// <summary>How many lines the live mirror currently holds — what the mirror input mapper clamps its
    /// scroll against.</summary>
    public int MirrorLineCount => _document.ToPseudoCode().Split('\n').Length;

    /// <summary>True while any modal interaction (text entry or the scene browser) is active — the
    /// window's click panels stand down until it ends.</summary>
    public bool IsModalActive => IsTextEntryActive || SceneBrowser is not null;

    /// <summary>Whether the F1 shortcut-reference overlay is shown. The composer reads it to build the
    /// overlay; <see cref="ToggleHelp"/> flips it.</summary>
    public bool HelpVisible { get; private set; }

    /// <summary>Shows or hides the help overlay (the F1 key).</summary>
    public void ToggleHelp() => HelpVisible = !HelpVisible;

    /// <summary>Whether the F3 developer stats overlay is shown. The composer reads it to build the
    /// overlay; <see cref="ToggleStats"/> flips it.</summary>
    public bool StatsVisible { get; private set; }

    /// <summary>Shows or hides the developer stats overlay (the F3 key).</summary>
    public void ToggleStats() => StatsVisible = !StatsVisible;

    /// <summary>Frames the selected element: the camera target moves to its world bounds' centre and the
    /// orbit distance fits those bounds (<see cref="CameraFraming"/>), so a tiny or distant element lands
    /// in view rather than just centred at the old zoom. With no selection it frames the whole visible
    /// scene; false only when there is nothing visible to frame.</summary>
    public bool FrameSelection()
    {
        if (_document.SelectedElements.Count > 1)
        {
            return CameraFraming.SelectionBounds(_document.Scene, _bounds, _document.SelectedElements)
                is { } selectionUnion && FrameBounds(selectionUnion);
        }

        var element = _document.SelectedElement;
        if (element.IsNode && _document.Scene.Find(element.AsNode) is { } node)
        {
            return FrameBounds(ScenePickList.WorldBoundsFor(_document.Scene, node, _bounds));
        }

        if (element.IsLight && _document.Scene.FindLight(element.AsLight) is { } light)
        {
            return FrameBounds(CameraFraming.LightGlyphBounds(light));
        }

        return CameraFraming.VisibleSceneBounds(_document.Scene, _bounds) is { } union && FrameBounds(union);
    }

    private bool FrameBounds(Aabb bounds)
    {
        Camera.Target = bounds.Centre;
        Camera.SetDistance(CameraFraming.FitDistance(bounds, Camera.FieldOfViewDegrees));
        return true;
    }

    /// <summary>A node's world-minus-local position offset (its parents' contribution to its position),
    /// captured at drag start so the gizmo / planar math can run in world space and write back the local
    /// transform. Zero for a root node, so an unparented drag is byte-identical.</summary>
    private Vector3 NodeWorldOffset(SceneNodeId id, EditorTransform local) =>
        SceneNodeTransforms.WorldMatrix(_document.Scene, id).Translation - local.Position.ToVector3();
}
