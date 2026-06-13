using static Opus.App.Editor.Cli.CliArgumentReader;
using static Opus.App.Editor.Cli.CliOptions;

namespace Opus.App.Editor.Cli;

/// <summary>Animation state-graph command parsers: add / remove a state, and wire / remove a transition.
/// State and transition parameters ride a focused <see cref="AnimationArgs"/> off the parsed args.</summary>
public static partial class EditorCliParser
{
    private static EditorArgs ParseAnimStateCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'anim-state' requires a graph path and a state name.");
        }

        if (!TryParseFloatOption(args, SpeedOption, 1f, out float speed))
        {
            return EditorArgs.ForHelp("Option --speed expects a number, e.g. --speed 1.5.");
        }

        var animation = new AnimationArgs(
            StateName: args[2],
            Clip: FindOptionValue(args, ClipOption),
            Loop: ParseLoopOption(args),
            Speed: speed,
            MakeEntry: HasFlag(args, EntryFlag));
        return new EditorArgs(EditorMode.AnimState, args[1], null, string.Empty, Animation: animation);
    }

    private static EditorArgs ParseAnimTransitionCommand(string[] args)
    {
        if (args.Length < 4 || string.IsNullOrWhiteSpace(args[1]) ||
            string.IsNullOrWhiteSpace(args[2]) || string.IsNullOrWhiteSpace(args[3]))
        {
            return EditorArgs.ForHelp("Command 'anim-transition' requires a graph path and from / to state names.");
        }

        string? trigger = FindOptionValue(args, OnOption);
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return EditorArgs.ForHelp("Command 'anim-transition' requires --on <trigger>.");
        }

        if (!TryParseFloatOption(args, BlendOption, 0f, out float blend))
        {
            return EditorArgs.ForHelp("Option --blend expects a number, e.g. --blend 0.2.");
        }

        var animation = new AnimationArgs(FromState: args[2], ToState: args[3], Trigger: trigger, Blend: blend);
        return new EditorArgs(EditorMode.AnimTransition, args[1], null, string.Empty, Animation: animation);
    }

    private static EditorArgs ParseAnimRemoveStateCommand(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            return EditorArgs.ForHelp("Command 'anim-remove-state' requires a graph path and a state name.");
        }

        return new EditorArgs(
            EditorMode.AnimRemoveState, args[1], null, string.Empty, Animation: new AnimationArgs(StateName: args[2]));
    }

    private static EditorArgs ParseAnimRemoveTransitionCommand(string[] args)
    {
        if (args.Length < 4 || string.IsNullOrWhiteSpace(args[1]) ||
            string.IsNullOrWhiteSpace(args[2]) || string.IsNullOrWhiteSpace(args[3]))
        {
            return EditorArgs.ForHelp(
                "Command 'anim-remove-transition' requires a graph path and from / to state names.");
        }

        string? trigger = FindOptionValue(args, OnOption);
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return EditorArgs.ForHelp("Command 'anim-remove-transition' requires --on <trigger>.");
        }

        var animation = new AnimationArgs(FromState: args[2], ToState: args[3], Trigger: trigger);
        return new EditorArgs(EditorMode.AnimRemoveTransition, args[1], null, string.Empty, Animation: animation);
    }
}
