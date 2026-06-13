using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// One editable (or displayed) property of the selected scene element in the inspector panel. The field
/// identifies the semantic slot — which component of which property — independent of element kind, so the
/// row builder, the click router, and the commit path all speak the same name.
/// </summary>
public enum InspectorField
{
    /// <summary>No field (a click missed every row).</summary>
    None,

    /// <summary>The element's display name (clicking it starts a rename).</summary>
    Name,

    /// <summary>A node's asset reference (display only).</summary>
    Asset,

    /// <summary>A light's kind (display only).</summary>
    Kind,

    /// <summary>Position X, in metres.</summary>
    PositionX,

    /// <summary>Position Y, in metres.</summary>
    PositionY,

    /// <summary>Position Z, in metres.</summary>
    PositionZ,

    /// <summary>Rotation about X, in degrees (nodes).</summary>
    RotationX,

    /// <summary>Rotation about Y, in degrees (nodes).</summary>
    RotationY,

    /// <summary>Rotation about Z, in degrees (nodes).</summary>
    RotationZ,

    /// <summary>Scale X (nodes).</summary>
    ScaleX,

    /// <summary>Scale Y (nodes).</summary>
    ScaleY,

    /// <summary>Scale Z (nodes).</summary>
    ScaleZ,

    /// <summary>Aim direction X (directional / spot lights).</summary>
    DirectionX,

    /// <summary>Aim direction Y (directional / spot lights).</summary>
    DirectionY,

    /// <summary>Aim direction Z (directional / spot lights).</summary>
    DirectionZ,

    /// <summary>Light colour red component (linear, nominally 0..1).</summary>
    ColorR,

    /// <summary>Light colour green component (linear, nominally 0..1).</summary>
    ColorG,

    /// <summary>Light colour blue component (linear, nominally 0..1).</summary>
    ColorB,

    /// <summary>Light intensity in the renderer's light-unit scale.</summary>
    Intensity,

    /// <summary>Light attenuation range in metres (point / spot).</summary>
    Range,

    /// <summary>Spot inner cone half-angle, in degrees.</summary>
    SpotInner,

    /// <summary>Spot outer cone half-angle, in degrees.</summary>
    SpotOuter,
}

/// <summary>
/// The in-progress numeric edit of one inspector field: which element and field, and the digits typed so
/// far. While active the window is modal exactly like a rename — keys feed this buffer, Enter commits the
/// parsed value as one undoable edit, Esc cancels. The buffer starts empty so the author types the new
/// value directly; the row keeps showing the old value as a placeholder until the first key.
/// </summary>
/// <param name="Element">The element whose field is being edited.</param>
/// <param name="Field">The field being edited.</param>
/// <param name="Buffer">The numeric text typed so far.</param>
public readonly record struct FieldEditState(SceneElementRef Element, InspectorField Field, string Buffer);
