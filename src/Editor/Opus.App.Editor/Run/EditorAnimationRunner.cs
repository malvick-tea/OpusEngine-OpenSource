using System;
using System.Globalization;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless animation-graph authoring for the editor CLI: create a graph, add states, wire transitions,
/// and inspect the result. Every mutation runs through the same <see cref="AnimationGraphEditor"/> command
/// path the visual editor will use, saves atomically, and prints the updated pseudo-code mirror — so the
/// campaign's animation orchestration can be assembled from the command line before the viewport exists.
/// </summary>
public static class EditorAnimationRunner
{
    public static int RunNew(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("anim-new requires a graph file path.");
            return EditorConsoleRunner.ExitUsage;
        }

        string name = ResolveGraphName(args);
        var saved = EditorAnimationFileStore.Save(args.ScenePath, AnimationGraphDocument.Empty(name));
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture, $"Created animation graph '{name}' at {args.ScenePath}."));
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunShow(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var editor = OpenGraph(args.ScenePath, "anim-show", log, out int code);
        if (editor is null)
        {
            return code;
        }

        var snapshot = editor.Snapshot();
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Animation graph '{snapshot.Name}' — {snapshot.States.Count} state(s), {snapshot.Transitions.Count} transition(s)."));
        PrintValidation(log, snapshot);
        log.Info(editor.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunAddState(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var anim = args.Animation;
        if (anim is null || string.IsNullOrWhiteSpace(anim.StateName))
        {
            log.Error("anim-state requires a graph path and a state name.");
            return EditorConsoleRunner.ExitUsage;
        }

        var editor = OpenGraph(args.ScenePath, "anim-state", log, out int code);
        if (editor is null)
        {
            return code;
        }

        var id = editor.AddState(anim.StateName, anim.Clip, anim.Loop, anim.Speed);
        if (anim.MakeEntry)
        {
            editor.SetEntryState(id);
        }

        if (!TrySave(args.ScenePath!, editor, log))
        {
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture, $"Added state '{anim.StateName}' (#{id}) to {args.ScenePath}."));
        log.Info(editor.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunAddTransition(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var anim = args.Animation;
        if (anim is null || string.IsNullOrWhiteSpace(anim.FromState) ||
            string.IsNullOrWhiteSpace(anim.ToState) || string.IsNullOrWhiteSpace(anim.Trigger))
        {
            log.Error("anim-transition requires a graph path, from/to state names, and --on <trigger>.");
            return EditorConsoleRunner.ExitUsage;
        }

        var editor = OpenGraph(args.ScenePath, "anim-transition", log, out int code);
        if (editor is null)
        {
            return code;
        }

        var from = editor.Graph.FindStateByName(anim.FromState);
        var to = editor.Graph.FindStateByName(anim.ToState);
        if (from is null || to is null)
        {
            log.Error($"Unknown state name '{(from is null ? anim.FromState : anim.ToState)}'.");
            return EditorConsoleRunner.ExitUsage;
        }

        editor.AddTransition(from.Id, to.Id, anim.Trigger, anim.Blend);
        if (!TrySave(args.ScenePath!, editor, log))
        {
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Wired '{anim.FromState}' -> '{anim.ToState}' on '{anim.Trigger}' in {args.ScenePath}."));
        log.Info(editor.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunRemoveState(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var anim = args.Animation;
        if (anim is null || string.IsNullOrWhiteSpace(anim.StateName))
        {
            log.Error("anim-remove-state requires a graph path and a state name.");
            return EditorConsoleRunner.ExitUsage;
        }

        var editor = OpenGraph(args.ScenePath, "anim-remove-state", log, out int code);
        if (editor is null)
        {
            return code;
        }

        var state = editor.Graph.FindStateByName(anim.StateName);
        if (state is null)
        {
            log.Error($"Unknown state name '{anim.StateName}'.");
            return EditorConsoleRunner.ExitUsage;
        }

        editor.RemoveState(state.Id);
        if (!TrySave(args.ScenePath!, editor, log))
        {
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture, $"Removed state '{anim.StateName}' (#{state.Id}) from {args.ScenePath}."));
        log.Info(editor.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunRemoveTransition(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var anim = args.Animation;
        if (anim is null || string.IsNullOrWhiteSpace(anim.FromState) ||
            string.IsNullOrWhiteSpace(anim.ToState) || string.IsNullOrWhiteSpace(anim.Trigger))
        {
            log.Error("anim-remove-transition requires a graph path, from/to state names, and --on <trigger>.");
            return EditorConsoleRunner.ExitUsage;
        }

        var editor = OpenGraph(args.ScenePath, "anim-remove-transition", log, out int code);
        if (editor is null)
        {
            return code;
        }

        var from = editor.Graph.FindStateByName(anim.FromState);
        var to = editor.Graph.FindStateByName(anim.ToState);
        if (from is null || to is null)
        {
            log.Error($"Unknown state name '{(from is null ? anim.FromState : anim.ToState)}'.");
            return EditorConsoleRunner.ExitUsage;
        }

        if (!editor.RemoveTransition(from.Id, to.Id, anim.Trigger))
        {
            log.Error($"No transition '{anim.FromState}' -> '{anim.ToState}' on '{anim.Trigger}' to remove.");
            return EditorConsoleRunner.ExitUsage;
        }

        if (!TrySave(args.ScenePath!, editor, log))
        {
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Removed transition '{anim.FromState}' -> '{anim.ToState}' on '{anim.Trigger}' from {args.ScenePath}."));
        log.Info(editor.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    private static AnimationGraphEditor? OpenGraph(string? path, string verb, ILog log, out int code)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            log.Error($"{verb} requires a graph file path.");
            code = EditorConsoleRunner.ExitUsage;
            return null;
        }

        var loaded = EditorAnimationFileStore.Load(path);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            code = EditorConsoleRunner.ExitIoFailed;
            return null;
        }

        var editor = new AnimationGraphEditor();
        editor.LoadGraph(loaded.Unwrap());
        code = EditorConsoleRunner.ExitOk;
        return editor;
    }

    private static bool TrySave(string path, AnimationGraphEditor editor, ILog log)
    {
        var saved = EditorAnimationFileStore.Save(path, editor.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return false;
        }

        return true;
    }

    private static void PrintValidation(ILog log, AnimationGraphDocument snapshot)
    {
        var issues = AnimationGraphValidator.Validate(snapshot);
        if (issues.Count == 0)
        {
            log.Info("Validation: ok.");
            return;
        }

        log.Info(string.Create(CultureInfo.InvariantCulture, $"Validation: {issues.Count} issue(s)."));
        foreach (var issue in issues)
        {
            log.Info($"  {issue.Kind}: {issue.Message}");
        }
    }

    private static string ResolveGraphName(EditorArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.SceneName))
        {
            return args.SceneName;
        }

        string stem = Path.GetFileNameWithoutExtension(args.ScenePath!);
        return string.IsNullOrWhiteSpace(stem) ? AnimationGraph.DefaultName : stem;
    }
}
