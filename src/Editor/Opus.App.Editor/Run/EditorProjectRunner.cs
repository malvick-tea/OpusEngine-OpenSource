using System;
using System.Globalization;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless editor-project management for the CLI: create a project manifest, add references to it, list
/// it, and check that every reference resolves on disk. This is the connective tissue that lets the editor
/// "work with the project" — content roots, scenes, animation graphs, and material roots gathered into one
/// validated workspace — assembled from the command line before the GUI exists.
/// </summary>
public static class EditorProjectRunner
{
    private const string SceneKind = "scene";
    private const string AnimGraphKind = "animgraph";
    private const string ContentRootKind = "content-root";
    private const string MaterialRootKind = "material-root";

    public static int RunNew(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("project-new requires a project file path.");
            return EditorConsoleRunner.ExitUsage;
        }

        string name = ResolveProjectName(args);
        var saved = EditorProjectFileStore.Save(args.ScenePath, EditorProjectDocument.Empty(name));
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(CultureInfo.InvariantCulture, $"Created project '{name}' at {args.ScenePath}."));
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunShow(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var loaded = LoadDocument(args.ScenePath, "project-show", log, out int code);
        if (loaded is null)
        {
            return code;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Project '{loaded.Name}' — {loaded.ContentRoots.Count} content root(s), {loaded.Scenes.Count} scene(s), {loaded.AnimationGraphs.Count} graph(s), {loaded.MaterialRoots.Count} material root(s)."));
        log.Info(EditorProjectDslWriter.Write(loaded));
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunAdd(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        string? kind = args.SceneName;
        string? path = args.AssetRef;
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(path))
        {
            log.Error("project-add requires a kind (scene|animgraph|content-root|material-root) and a path.");
            return EditorConsoleRunner.ExitUsage;
        }

        var loaded = LoadDocument(args.ScenePath, "project-add", log, out int code);
        if (loaded is null)
        {
            return code;
        }

        var project = new EditorProject();
        project.Load(loaded);
        if (!TryAdd(project, kind, path, out bool added))
        {
            log.Error($"Unknown kind '{kind}'. Use scene | animgraph | content-root | material-root.");
            return EditorConsoleRunner.ExitUsage;
        }

        if (!added)
        {
            log.Info($"'{path}' is already a {kind} in {args.ScenePath}.");
            return EditorConsoleRunner.ExitOk;
        }

        var saved = EditorProjectFileStore.Save(args.ScenePath!, project.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(CultureInfo.InvariantCulture, $"Added {kind} '{path}' to {args.ScenePath}."));
        log.Info(EditorProjectDslWriter.Write(project.Snapshot()));
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunCheck(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var loaded = LoadDocument(args.ScenePath, "project-check", log, out int code);
        if (loaded is null)
        {
            return code;
        }

        string baseDir = ResolveBaseDirectory(args);
        var issues = EditorProjectValidator.Validate(loaded, path => Path.Exists(Path.Combine(baseDir, path)));
        if (issues.Count == 0)
        {
            log.Info(string.Create(
                CultureInfo.InvariantCulture, $"Project '{loaded.Name}': all references resolve under {baseDir}."));
            return EditorConsoleRunner.ExitOk;
        }

        log.Info(string.Create(CultureInfo.InvariantCulture, $"Project '{loaded.Name}': {issues.Count} missing reference(s)."));
        foreach (var issue in issues)
        {
            log.Info($"  {issue.Kind}: {issue.Path}");
        }

        return EditorConsoleRunner.ExitOk;
    }

    private static bool TryAdd(EditorProject project, string kind, string path, out bool added)
    {
        added = false;
        switch (kind)
        {
            case SceneKind:
                added = project.AddScene(path);
                return true;
            case AnimGraphKind:
                added = project.AddAnimationGraph(path);
                return true;
            case ContentRootKind:
                added = project.AddContentRoot(path);
                return true;
            case MaterialRootKind:
                added = project.AddMaterialRoot(path);
                return true;
            default:
                return false;
        }
    }

    private static EditorProjectDocument? LoadDocument(string? path, string verb, ILog log, out int code)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            log.Error($"{verb} requires a project file path.");
            code = EditorConsoleRunner.ExitUsage;
            return null;
        }

        var loaded = EditorProjectFileStore.Load(path);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            code = EditorConsoleRunner.ExitIoFailed;
            return null;
        }

        code = EditorConsoleRunner.ExitOk;
        return loaded.Unwrap();
    }

    private static string ResolveBaseDirectory(EditorArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.ContentRoot))
        {
            return args.ContentRoot;
        }

        return Path.GetDirectoryName(Path.GetFullPath(args.ScenePath!)) ?? ".";
    }

    private static string ResolveProjectName(EditorArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.SceneName))
        {
            return args.SceneName;
        }

        string stem = Path.GetFileNameWithoutExtension(args.ScenePath!);
        return string.IsNullOrWhiteSpace(stem) ? EditorProject.DefaultName : stem;
    }
}
