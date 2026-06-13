using static Opus.App.Editor.Cli.CliArgumentReader;
using static Opus.App.Editor.Cli.CliOptions;

namespace Opus.App.Editor.Cli;

/// <summary>Editor-project command parsers: add a reference, and the path-with-optional-content-root shape
/// shared by project-check and project-doctor. (project-new / project-show reuse the generic scene-command
/// parser through the dispatch table.)</summary>
public static partial class EditorCliParser
{
    private static EditorArgs ParseProjectAddCommand(string[] args)
    {
        if (args.Length < 4 || string.IsNullOrWhiteSpace(args[1]) ||
            string.IsNullOrWhiteSpace(args[2]) || string.IsNullOrWhiteSpace(args[3]))
        {
            return EditorArgs.ForHelp(
                "Command 'project-add' requires a project path, a kind (scene|animgraph|content-root|material-root), and a path.");
        }

        return new EditorArgs(EditorMode.ProjectAdd, args[1], args[2], string.Empty, AssetRef: args[3]);
    }

    private static EditorArgs ParseProjectPathWithRoot(EditorMode mode, string verb, string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            return EditorArgs.ForHelp($"Command '{verb}' requires a project file path.");
        }

        return new EditorArgs(
            mode, args[1], null, string.Empty, ContentRoot: FindOptionValue(args, ContentRootOption));
    }
}
