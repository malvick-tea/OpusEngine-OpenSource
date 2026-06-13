using System;
using System.Collections.Generic;
using System.Globalization;
using Opus.Editor.Ui;
using static Opus.App.Editor.Cli.CliArgumentReader;
using static Opus.App.Editor.Cli.CliOptions;

namespace Opus.App.Editor.Cli;

/// <summary>
/// Parses the editor CLI argument vector into an <see cref="EditorArgs"/>. Pure and total: an unknown
/// command, a missing required argument, or a malformed option resolves to Help with a reason rather than
/// throwing, so the entry point can print usage and return a non-zero exit code. Dispatch is table-driven —
/// <see cref="Commands"/> pairs each verb with its parser and its help usage, the single source of truth the
/// help banner also reads. The per-command parsers live in family partial files (scene / light / anim /
/// project); the value readers live in <see cref="CliArgumentReader"/>.
/// </summary>
public static partial class EditorCliParser
{
    /// <summary>The dispatch + help table: every sub-command verb, its parser, and its usage lines, in the
    /// order the help banner lists them. The window verb is here for help and explicit invocation; the
    /// no-argument default opens the window too (handled in <see cref="Parse"/>).</summary>
    public static readonly IReadOnlyList<EditorCommand> Commands = new[]
    {
        Cmd("new", a => ParseSceneCommand(EditorMode.New, a),
            "  opus-editor new <scene> [--name <name>]  Create a new empty scene document file."),
        Cmd("show", a => ParseSceneCommand(EditorMode.Show, a),
            "  opus-editor show <scene>                 Print a scene summary and its pseudo-code."),
        Cmd("dsl", a => ParseSceneCommand(EditorMode.Dsl, a),
            "  opus-editor dsl <scene>                  Print only a scene's pseudo-code (DSL) mirror."),
        Cmd("inspect", a => ParseSceneCommand(EditorMode.Inspect, a),
            "  opus-editor inspect <model>              Summarise an imported glTF/GLB model."),
        Cmd("place", ParsePlaceCommand,
            "  opus-editor place <scene> <asset> [--name <n>] [--at x,y,z]",
            "                                           Place a model into a scene and print the result."),
        Cmd("scene-remove", a => ParseSceneNodeCommand(EditorMode.SceneRemove, a),
            "  opus-editor scene-remove <scene> <node-id>",
            "                                           Remove a node from a scene by id."),
        Cmd("scene-rename", ParseSceneRenameCommand,
            "  opus-editor scene-rename <scene> <node-id> <new-name>",
            "                                           Rename a scene node by id."),
        Cmd("scene-move", ParseSceneMoveCommand,
            "  opus-editor scene-move <scene> <node-id> --at x,y,z",
            "                                           Move a scene node by id to a new position."),
        Cmd("scene-rotate", ParseSceneRotateCommand,
            "  opus-editor scene-rotate <scene> <node-id> --euler yaw,pitch,roll",
            "                                           Set a scene node's rotation (Euler degrees) by id."),
        Cmd("scene-scale", ParseSceneScaleCommand,
            "  opus-editor scene-scale <scene> <node-id> --scale sx,sy,sz",
            "                                           Set a scene node's scale by id (components non-zero)."),
        Cmd("scene-duplicate", ParseSceneDuplicateCommand,
            "  opus-editor scene-duplicate <scene> <node-id> [--at x,y,z]",
            "                                           Duplicate a scene node by id (offset, or at --at)."),
        Cmd("scene-parent", ParseSceneParentCommand,
            "  opus-editor scene-parent <scene> <child-id> <parent-id>",
            "                                           Parent a node under another (rejects cycles)."),
        Cmd("scene-unparent", a => ParseSceneNodeCommand(EditorMode.SceneUnparent, a),
            "  opus-editor scene-unparent <scene> <node-id>",
            "                                           Detach a node to a root."),
        Cmd("light-add", ParseLightAddCommand,
            "  opus-editor light-add <scene> <directional|point|spot> [--name <n>] [--color r,g,b]",
            "             [--intensity <i>] [--at x,y,z] [--dir x,y,z] [--range <r>] [--cone inner,outer]",
            "                                           Add a light to a scene (per-kind fields default if omitted)."),
        Cmd("light-remove", ParseLightRemoveCommand,
            "  opus-editor light-remove <scene> <light-id>",
            "                                           Remove a light from a scene by id."),
        Cmd("light-edit", ParseLightEditCommand,
            "  opus-editor light-edit <scene> <light-id> [--name <n>] [--color r,g,b] [--intensity <i>]",
            "             [--at x,y,z] [--dir x,y,z] [--range <r>] [--cone inner,outer]",
            "                                           Retune an existing light by id (only the given fields change)."),
        Cmd("report", ParseReportCommand,
            "  opus-editor report <scene> [--content-root <dir>]",
            "                                           Report a scene's content cost (assets / instances / triangles)."),
        Cmd("materials", ParseMaterialsCommand,
            "  opus-editor materials <root> [--name <material>]",
            "                                           Validate PBR material sets on disk (which maps are authored)."),
        Cmd("anim-new", a => ParseSceneCommand(EditorMode.AnimNew, a),
            "  opus-editor anim-new <graph> [--name <name>]",
            "                                           Create a new empty animation state-graph document."),
        Cmd("anim-show", a => ParseSceneCommand(EditorMode.AnimShow, a),
            "  opus-editor anim-show <graph>            Print an animation graph's summary, validation, and pseudo-code."),
        Cmd("anim-state", ParseAnimStateCommand,
            "  opus-editor anim-state <graph> <name> [--clip <ref>] [--loop true|false] [--speed <s>] [--entry]",
            "                                           Add a state to an animation graph."),
        Cmd("anim-transition", ParseAnimTransitionCommand,
            "  opus-editor anim-transition <graph> <from> <to> --on <trigger> [--blend <s>]",
            "                                           Wire a transition between two states."),
        Cmd("anim-remove-state", ParseAnimRemoveStateCommand,
            "  opus-editor anim-remove-state <graph> <name>",
            "                                           Remove a state (and its transitions) from a graph."),
        Cmd("anim-remove-transition", ParseAnimRemoveTransitionCommand,
            "  opus-editor anim-remove-transition <graph> <from> <to> --on <trigger>",
            "                                           Remove a transition between two states."),
        Cmd("project-new", a => ParseSceneCommand(EditorMode.ProjectNew, a),
            "  opus-editor project-new <project> [--name <name>]",
            "                                           Create a new empty editor-project manifest."),
        Cmd("project-show", a => ParseSceneCommand(EditorMode.ProjectShow, a),
            "  opus-editor project-show <project>       Print a project's summary and pseudo-code."),
        Cmd("project-add", ParseProjectAddCommand,
            "  opus-editor project-add <project> <kind> <path>",
            "                                           Add a scene|animgraph|content-root|material-root reference."),
        Cmd("project-check", a => ParseProjectPathWithRoot(EditorMode.ProjectCheck, "project-check", a),
            "  opus-editor project-check <project> [--content-root <dir>]",
            "                                           Check that every project reference resolves on disk."),
        Cmd("project-doctor", a => ParseProjectPathWithRoot(EditorMode.ProjectDoctor, "project-doctor", a),
            "  opus-editor project-doctor <project> [--content-root <dir>]",
            "                                           Open and validate every scene / graph / material set in a project."),
        Cmd("window", ParseWindowCommand,
            "  opus-editor window [<scene>] [--project <file>] [--content-root <dir>] [--settings <file>] [--lang en|ru] [--frames <n>]",
            "                                           Open the live D3D12 authoring window (orbit / select / move / pseudo-code mirror).",
            "                                           --settings persists window size, last scene, last project, and chosen --lang across runs; --lang sets the chrome language.",
            "                                           --project opens the window on a project: its content roots feed the place-model browser (M),",
            "                                           its scenes join the open browser (Ctrl+O), and without a scene argument its first scene opens."),
    };

    public static EditorArgs Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0)
        {
            return ParseWindowCommand(args);
        }

        string command = args[0];
        if (command is "help" or "--help" or "-h")
        {
            return EditorArgs.ForHelp(string.Empty);
        }

        foreach (var entry in Commands)
        {
            if (string.Equals(entry.Name, command, StringComparison.Ordinal))
            {
                return entry.Parse(args);
            }
        }

        return EditorArgs.ForHelp($"Unknown command '{command}'.");
    }

    private static EditorCommand Cmd(string name, Func<string[], EditorArgs> parse, params string[] usage) =>
        new(name, parse, usage);

    private static EditorArgs ParseWindowCommand(string[] args)
    {
        string? scenePath = args.Length >= 2 && !args[1].StartsWith("--", StringComparison.Ordinal)
            ? args[1]
            : null;

        int frames = 0;
        string? framesText = FindOptionValue(args, FramesOption);
        if (framesText is not null &&
            (!int.TryParse(framesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out frames) || frames < 0))
        {
            return EditorArgs.ForHelp("Option --frames expects a non-negative integer.");
        }

        if (!TryParseLanguage(FindOptionValue(args, LanguageOption), out var language))
        {
            return EditorArgs.ForHelp("Option --lang expects 'en' or 'ru'.");
        }

        return new EditorArgs(
            EditorMode.Window,
            scenePath,
            null,
            string.Empty,
            ContentRoot: FindOptionValue(args, ContentRootOption),
            WindowMaxFrames: frames,
            SettingsPath: FindOptionValue(args, SettingsOption),
            Language: language,
            ProjectPath: FindOptionValue(args, ProjectOption));
    }

    private static bool TryParseLanguage(string? text, out EditorLanguage? language)
    {
        language = null;
        if (text is null)
        {
            return true;
        }

        switch (text.ToLowerInvariant())
        {
            case "en":
            case "english":
                language = EditorLanguage.English;
                return true;
            case "ru":
            case "russian":
                language = EditorLanguage.Russian;
                return true;
            default:
                return false;
        }
    }
}
