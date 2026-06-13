using Opus.Editor.Core;
using static Opus.App.Editor.Cli.CliArgumentReader;
using static Opus.App.Editor.Cli.CliOptions;

namespace Opus.App.Editor.Cli;

/// <summary>Light command parsers: light-add (kind required, per-kind fields default when omitted),
/// light-remove (by id), and light-edit (every field optional; the kind is kept).</summary>
public static partial class EditorCliParser
{
    private static EditorArgs ParseLightAddCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'light-add' requires a scene path and a kind (directional|point|spot).");
        }

        if (!TryParseLightKind(args[2], out var kind))
        {
            return EditorArgs.ForHelp("Light kind must be one of directional, point, or spot.");
        }

        if (TryReadLightFields(args, out var fields) is { } error)
        {
            return error;
        }

        var light = new LightArgs(kind, fields.Color, fields.Intensity, fields.Direction, fields.Range, fields.Inner, fields.Outer);
        return new EditorArgs(
            EditorMode.LightAdd, args[1], FindOptionValue(args, NameOption), string.Empty,
            Position: fields.Position, Light: light);
    }

    private static EditorArgs ParseLightRemoveCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'light-remove' requires a scene path and a light id.");
        }

        return new EditorArgs(EditorMode.LightRemove, args[1], null, string.Empty, AssetRef: args[2]);
    }

    private static EditorArgs ParseLightEditCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'light-edit' requires a scene path and a light id.");
        }

        if (TryReadLightFields(args, out var fields) is { } error)
        {
            return error;
        }

        // Kind is left null: light-edit keeps the existing light's kind.
        var light = new LightArgs(null, fields.Color, fields.Intensity, fields.Direction, fields.Range, fields.Inner, fields.Outer);
        return new EditorArgs(
            EditorMode.LightEdit, args[1], FindOptionValue(args, NameOption), string.Empty,
            AssetRef: args[2], Position: fields.Position, Light: light);
    }

    /// <summary>The optional light field options shared by light-add and light-edit.</summary>
    private readonly record struct LightFields(
        Float3? Position, Float3? Color, Float3? Direction, float? Intensity, float? Range, float? Inner, float? Outer);

    /// <summary>Reads the optional light field options (<c>--at / --color / --dir / --intensity / --range /
    /// --cone</c>) shared by light-add and light-edit; returns a Help <see cref="EditorArgs"/> naming the
    /// first malformed option, or null when every present option parsed.</summary>
    private static EditorArgs? TryReadLightFields(string[] args, out LightFields fields)
    {
        fields = default;
        if (!TryParseOptionalFloat3(args, AtOption, out var position))
        {
            return EditorArgs.ForHelp("Option --at expects three comma-separated numbers, e.g. --at 10,8,0.");
        }

        if (!TryParseOptionalFloat3(args, ColorOption, out var color))
        {
            return EditorArgs.ForHelp("Option --color expects three comma-separated numbers, e.g. --color 1,0.8,0.6.");
        }

        if (!TryParseOptionalFloat3(args, DirectionOption, out var direction))
        {
            return EditorArgs.ForHelp("Option --dir expects three comma-separated numbers, e.g. --dir 0,-1,0.");
        }

        if (!TryParseOptionalFloat(args, IntensityOption, out float? intensity))
        {
            return EditorArgs.ForHelp("Option --intensity expects a number, e.g. --intensity 2.");
        }

        if (!TryParseOptionalFloat(args, RangeOption, out float? range))
        {
            return EditorArgs.ForHelp("Option --range expects a number, e.g. --range 12.");
        }

        if (!TryParseOptionalCone(args, ConeOption, out float? inner, out float? outer))
        {
            return EditorArgs.ForHelp("Option --cone expects two comma-separated degrees, e.g. --cone 20,30.");
        }

        fields = new LightFields(position, color, direction, intensity, range, inner, outer);
        return null;
    }
}
