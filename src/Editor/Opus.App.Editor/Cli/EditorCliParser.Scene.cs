using Opus.Editor.Core;
using static Opus.App.Editor.Cli.CliArgumentReader;
using static Opus.App.Editor.Cli.CliOptions;

namespace Opus.App.Editor.Cli;

/// <summary>Scene-document and node-edit command parsers: new / show / dsl / inspect, place, the scene-edit
/// family (remove / rename / move / rotate / scale / duplicate / parent / unparent), report, and
/// materials.</summary>
public static partial class EditorCliParser
{
    private static EditorArgs ParseSceneCommand(EditorMode mode, string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            return EditorArgs.ForHelp($"Command '{args[0]}' requires a scene file path.");
        }

        return new EditorArgs(mode, args[1], FindOptionValue(args, NameOption), string.Empty);
    }

    private static EditorArgs ParsePlaceCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'place' requires a scene path and an asset reference.");
        }

        if (!TryParseOptionalFloat3(args, AtOption, out var position))
        {
            return EditorArgs.ForHelp("Option --at expects three comma-separated numbers, e.g. --at 10,0,5.");
        }

        return new EditorArgs(
            EditorMode.Place, args[1], FindOptionValue(args, NameOption), string.Empty, args[2], position);
    }

    private static EditorArgs ParseSceneNodeCommand(EditorMode mode, string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp($"Command '{args[0]}' requires a scene path and a node id.");
        }

        return new EditorArgs(mode, args[1], null, string.Empty, AssetRef: args[2]);
    }

    private static EditorArgs ParseSceneParentCommand(string[] args)
    {
        if (args.Length < 4 || string.IsNullOrWhiteSpace(args[1]) ||
            string.IsNullOrWhiteSpace(args[2]) || string.IsNullOrWhiteSpace(args[3]))
        {
            return EditorArgs.ForHelp(
                "Command 'scene-parent' requires a scene path, a child node id, and a parent node id.");
        }

        // Child id rides AssetRef (like the other scene-edit commands); parent id rides SceneName, the
        // spare string carrier scene-rename already uses for its third positional.
        return new EditorArgs(EditorMode.SceneParent, args[1], args[3], string.Empty, AssetRef: args[2]);
    }

    private static EditorArgs ParseSceneRenameCommand(string[] args)
    {
        if (args.Length < 4 || string.IsNullOrWhiteSpace(args[1]) ||
            string.IsNullOrWhiteSpace(args[2]) || string.IsNullOrWhiteSpace(args[3]))
        {
            return EditorArgs.ForHelp("Command 'scene-rename' requires a scene path, a node id, and a new name.");
        }

        return new EditorArgs(EditorMode.SceneRename, args[1], args[3], string.Empty, AssetRef: args[2]);
    }

    private static EditorArgs ParseSceneMoveCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'scene-move' requires a scene path and a node id.");
        }

        string? at = FindOptionValue(args, AtOption);
        if (at is null || !TryParseFloat3(at, out var position))
        {
            return EditorArgs.ForHelp("Command 'scene-move' requires --at x,y,z (three comma-separated numbers).");
        }

        return new EditorArgs(EditorMode.SceneMove, args[1], null, string.Empty, AssetRef: args[2], Position: position);
    }

    private static EditorArgs ParseSceneRotateCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'scene-rotate' requires a scene path and a node id.");
        }

        string? euler = FindOptionValue(args, EulerOption);
        if (euler is null || !TryParseFloat3(euler, out var rotation))
        {
            return EditorArgs.ForHelp(
                "Command 'scene-rotate' requires --euler yaw,pitch,roll (three comma-separated degrees).");
        }

        return new EditorArgs(EditorMode.SceneRotate, args[1], null, string.Empty, AssetRef: args[2], Position: rotation);
    }

    private static EditorArgs ParseSceneScaleCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'scene-scale' requires a scene path and a node id.");
        }

        string? scaleText = FindOptionValue(args, ScaleOption);
        if (scaleText is null || !TryParseFloat3(scaleText, out var scale))
        {
            return EditorArgs.ForHelp(
                "Command 'scene-scale' requires --scale sx,sy,sz (three comma-separated factors).");
        }

        if (scale.X == 0f || scale.Y == 0f || scale.Z == 0f)
        {
            return EditorArgs.ForHelp("Option --scale components must be non-zero (a zero scale collapses the node).");
        }

        return new EditorArgs(EditorMode.SceneScale, args[1], null, string.Empty, AssetRef: args[2], Position: scale);
    }

    private static EditorArgs ParseSceneDuplicateCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'scene-duplicate' requires a scene path and a node id.");
        }

        if (!TryParseOptionalFloat3(args, AtOption, out var position))
        {
            return EditorArgs.ForHelp("Option --at expects three comma-separated numbers, e.g. --at 10,0,5.");
        }

        return new EditorArgs(
            EditorMode.SceneDuplicate, args[1], null, string.Empty, AssetRef: args[2], Position: position);
    }

    private static EditorArgs ParseReportCommand(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            return EditorArgs.ForHelp("Command 'report' requires a scene file path.");
        }

        return new EditorArgs(
            EditorMode.Report, args[1], null, string.Empty, ContentRoot: FindOptionValue(args, ContentRootOption));
    }

    private static EditorArgs ParseMaterialsCommand(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            return EditorArgs.ForHelp("Command 'materials' requires a textures root directory.");
        }

        return new EditorArgs(EditorMode.Materials, args[1], FindOptionValue(args, NameOption), string.Empty);
    }
}
