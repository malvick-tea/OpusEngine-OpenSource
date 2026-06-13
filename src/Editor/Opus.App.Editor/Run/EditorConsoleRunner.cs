using System;
using System.Globalization;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless console operations for the editor CLI: create a new scene file, and load and print a scene's
/// summary and pseudo-code mirror. Writes through an <see cref="ILog"/> and returns conventional process
/// exit codes. These modes prove the document core, serialisation, and the pseudo-code mirror end to end
/// without a GPU; the visual authoring window is a later EM phase.
/// </summary>
public static class EditorConsoleRunner
{
    public const int ExitOk = 0;
    public const int ExitUsage = 1;
    public const int ExitIoFailed = 2;

    public static int RunNew(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("new requires a scene file path.");
            return ExitUsage;
        }

        string name = ResolveName(args);
        var saved = EditorSceneFileStore.Save(args.ScenePath, EditorSceneDocument.Empty(name));
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return ExitIoFailed;
        }

        log.Info(string.Create(CultureInfo.InvariantCulture, $"Created scene '{name}' at {args.ScenePath}."));
        return ExitOk;
    }

    public static int RunShow(EditorArgs args, ILog log, bool dslOnly)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("show requires a scene file path.");
            return ExitUsage;
        }

        var loaded = EditorSceneFileStore.Load(args.ScenePath);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            return ExitIoFailed;
        }

        var document = loaded.Unwrap();
        if (!dslOnly)
        {
            log.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"Scene '{document.Name}' — {document.Nodes.Count} node(s), {document.Lights.Count} light(s)."));
        }

        log.Info(SceneDslWriter.Write(document));
        return ExitOk;
    }

    private static string ResolveName(EditorArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.SceneName))
        {
            return args.SceneName;
        }

        string stem = Path.GetFileNameWithoutExtension(args.ScenePath!);
        return string.IsNullOrWhiteSpace(stem) ? EditorScene.DefaultName : stem;
    }
}
