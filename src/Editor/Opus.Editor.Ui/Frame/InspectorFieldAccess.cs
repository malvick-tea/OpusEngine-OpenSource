using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// Reads and writes the numeric inspector fields of scene elements: which value a field currently holds,
/// and the element with that field replaced. One place owns the field-to-property mapping so the row
/// builder (display) and the controller's commit path (edit) can never disagree about what a field means.
/// Pure and total — an inapplicable (element, field) pair reads as null and applies as null.
/// </summary>
public static class InspectorFieldAccess
{
    /// <summary>The field's current value on a node's transform, or null when the field does not apply to
    /// nodes.</summary>
    public static float? Read(EditorTransform transform, InspectorField field) => field switch
    {
        InspectorField.PositionX => transform.Position.X,
        InspectorField.PositionY => transform.Position.Y,
        InspectorField.PositionZ => transform.Position.Z,
        InspectorField.RotationX => transform.RotationEulerDegrees.X,
        InspectorField.RotationY => transform.RotationEulerDegrees.Y,
        InspectorField.RotationZ => transform.RotationEulerDegrees.Z,
        InspectorField.ScaleX => transform.Scale.X,
        InspectorField.ScaleY => transform.Scale.Y,
        InspectorField.ScaleZ => transform.Scale.Z,
        _ => null,
    };

    /// <summary>The transform with the field set to <paramref name="value"/>, or null when the field does
    /// not apply to nodes.</summary>
    public static EditorTransform? Apply(EditorTransform transform, InspectorField field, float value) => field switch
    {
        InspectorField.PositionX => transform with { Position = transform.Position with { X = value } },
        InspectorField.PositionY => transform with { Position = transform.Position with { Y = value } },
        InspectorField.PositionZ => transform with { Position = transform.Position with { Z = value } },
        InspectorField.RotationX => transform with { RotationEulerDegrees = transform.RotationEulerDegrees with { X = value } },
        InspectorField.RotationY => transform with { RotationEulerDegrees = transform.RotationEulerDegrees with { Y = value } },
        InspectorField.RotationZ => transform with { RotationEulerDegrees = transform.RotationEulerDegrees with { Z = value } },
        InspectorField.ScaleX => transform with { Scale = transform.Scale with { X = value } },
        InspectorField.ScaleY => transform with { Scale = transform.Scale with { Y = value } },
        InspectorField.ScaleZ => transform with { Scale = transform.Scale with { Z = value } },
        _ => null,
    };

    /// <summary>The field's current value on a light, or null when the field does not apply to that
    /// light's kind — a directional light has no position or range, a point light no direction or cone.</summary>
    public static float? Read(SceneLight light, InspectorField field) => field switch
    {
        InspectorField.PositionX when HasPosition(light) => light.Position.X,
        InspectorField.PositionY when HasPosition(light) => light.Position.Y,
        InspectorField.PositionZ when HasPosition(light) => light.Position.Z,
        InspectorField.DirectionX when HasDirection(light) => light.Direction.X,
        InspectorField.DirectionY when HasDirection(light) => light.Direction.Y,
        InspectorField.DirectionZ when HasDirection(light) => light.Direction.Z,
        InspectorField.ColorR => light.Color.X,
        InspectorField.ColorG => light.Color.Y,
        InspectorField.ColorB => light.Color.Z,
        InspectorField.Intensity => light.Intensity,
        InspectorField.Range when HasPosition(light) => light.Range,
        InspectorField.SpotInner when light.Kind == SceneLightKind.Spot => light.SpotInnerAngleDegrees,
        InspectorField.SpotOuter when light.Kind == SceneLightKind.Spot => light.SpotOuterAngleDegrees,
        _ => null,
    };

    /// <summary>The light with the field set to <paramref name="value"/>, or null when the field does not
    /// apply to that light's kind.</summary>
    public static SceneLight? Apply(SceneLight light, InspectorField field, float value) => field switch
    {
        InspectorField.PositionX when HasPosition(light) => light with { Position = light.Position with { X = value } },
        InspectorField.PositionY when HasPosition(light) => light with { Position = light.Position with { Y = value } },
        InspectorField.PositionZ when HasPosition(light) => light with { Position = light.Position with { Z = value } },
        InspectorField.DirectionX when HasDirection(light) => light with { Direction = light.Direction with { X = value } },
        InspectorField.DirectionY when HasDirection(light) => light with { Direction = light.Direction with { Y = value } },
        InspectorField.DirectionZ when HasDirection(light) => light with { Direction = light.Direction with { Z = value } },
        InspectorField.ColorR => light with { Color = light.Color with { X = value } },
        InspectorField.ColorG => light with { Color = light.Color with { Y = value } },
        InspectorField.ColorB => light with { Color = light.Color with { Z = value } },
        InspectorField.Intensity => light with { Intensity = value },
        InspectorField.Range when HasPosition(light) => light with { Range = value },
        InspectorField.SpotInner when light.Kind == SceneLightKind.Spot => light with { SpotInnerAngleDegrees = value },
        InspectorField.SpotOuter when light.Kind == SceneLightKind.Spot => light with { SpotOuterAngleDegrees = value },
        _ => null,
    };

    /// <summary>Whether the light kind carries a world position (point and spot lights).</summary>
    private static bool HasPosition(SceneLight light) => light.Kind != SceneLightKind.Directional;

    /// <summary>Whether the light kind carries an aim direction (directional and spot lights).</summary>
    private static bool HasDirection(SceneLight light) => light.Kind != SceneLightKind.Point;
}
