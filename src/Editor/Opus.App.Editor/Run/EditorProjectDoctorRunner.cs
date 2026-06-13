using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Content;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless whole-project health check for the editor CLI: load a project manifest and, for every
/// reference it lists, actually open and validate the content — scenes load, animation graphs validate,
/// material sets are scanned for completeness, content roots exist. Prints a per-item status with a problem
/// count. This is the "implementation information for developers" view at the project level, reusing the
/// same pure cores the individual commands do.
/// </summary>
public static class EditorProjectDoctorRunner
{
    public static int RunDoctor(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("project-doctor requires a project file path.");
            return EditorConsoleRunner.ExitUsage;
        }

        var loaded = EditorProjectFileStore.Load(args.ScenePath);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        var project = loaded.Unwrap();
        string baseDir = ResolveBaseDirectory(args);
        var contentRoots = ResolveContentRoots(project, baseDir);
        log.Info(string.Create(CultureInfo.InvariantCulture, $"Project '{project.Name}' health (root {baseDir}):"));

        int items = 0;
        int problems = 0;
        void Emit((string Line, bool Problem) result)
        {
            log.Info(result.Line);
            items++;
            if (result.Problem)
            {
                problems++;
            }
        }

        foreach (string path in project.ContentRoots)
        {
            Emit(CheckContentRoot(baseDir, path));
        }

        foreach (string path in project.Scenes)
        {
            Emit(CheckScene(baseDir, path, contentRoots));
        }

        foreach (string path in project.AnimationGraphs)
        {
            Emit(CheckAnimationGraph(baseDir, path));
        }

        foreach (string path in project.MaterialRoots)
        {
            Emit(CheckMaterialRoot(baseDir, path));
        }

        log.Info(string.Create(CultureInfo.InvariantCulture, $"Summary: {items} item(s), {problems} problem(s)."));
        return EditorConsoleRunner.ExitOk;
    }

    private static (string Line, bool Problem) CheckContentRoot(string baseDir, string path)
    {
        bool exists = Directory.Exists(Path.Combine(baseDir, path));
        return exists
            ? ($"  content-root \"{path}\" [ok]", false)
            : ($"  content-root \"{path}\" [FAIL] — directory not found", true);
    }

    private static (string Line, bool Problem) CheckScene(
        string baseDir, string path, IReadOnlyList<string> contentRoots)
    {
        var loaded = EditorSceneFileStore.Load(Path.Combine(baseDir, path));
        if (loaded.IsErr)
        {
            return ($"  scene \"{path}\" [FAIL] — {loaded.UnwrapErr().Message}", true);
        }

        var document = loaded.Unwrap();
        int nodes = document.Nodes.Count;
        int unresolved = CountUnresolvedAssets(document, contentRoots);
        if (unresolved == 0)
        {
            return (string.Create(
                CultureInfo.InvariantCulture, $"  scene \"{path}\" [ok] — {nodes} node(s), 0 unresolved asset(s)"), false);
        }

        return (string.Create(
            CultureInfo.InvariantCulture, $"  scene \"{path}\" [WARN] — {nodes} node(s), {unresolved} unresolved asset(s)"), true);
    }

    private static int CountUnresolvedAssets(EditorSceneDocument document, IReadOnlyList<string> contentRoots)
    {
        int unresolved = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in document.Nodes)
        {
            if (node.AssetRef is not { } assetRef || !seen.Add(assetRef))
            {
                continue;
            }

            if (!ResolvesInAnyRoot(assetRef, contentRoots))
            {
                unresolved++;
            }
        }

        return unresolved;
    }

    private static bool ResolvesInAnyRoot(string assetRef, IReadOnlyList<string> contentRoots)
    {
        foreach (string root in contentRoots)
        {
            if (File.Exists(Path.Combine(root, assetRef)))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ResolveContentRoots(EditorProjectDocument project, string baseDir)
    {
        var roots = new List<string>(project.ContentRoots.Count);
        foreach (string root in project.ContentRoots)
        {
            roots.Add(Path.Combine(baseDir, root));
        }

        return roots;
    }

    private static (string Line, bool Problem) CheckAnimationGraph(string baseDir, string path)
    {
        var loaded = EditorAnimationFileStore.Load(Path.Combine(baseDir, path));
        if (loaded.IsErr)
        {
            return ($"  animgraph \"{path}\" [FAIL] — {loaded.UnwrapErr().Message}", true);
        }

        var document = loaded.Unwrap();
        var issues = AnimationGraphValidator.Validate(document);
        if (issues.Count == 0)
        {
            return (string.Create(
                CultureInfo.InvariantCulture, $"  animgraph \"{path}\" [ok] — {document.States.Count} state(s), 0 issue(s)"), false);
        }

        return (string.Create(
            CultureInfo.InvariantCulture, $"  animgraph \"{path}\" [WARN] — {issues.Count} issue(s)"), true);
    }

    private static (string Line, bool Problem) CheckMaterialRoot(string baseDir, string path)
    {
        string root = Path.Combine(baseDir, path);
        if (!Directory.Exists(root))
        {
            return ($"  material-root \"{path}\" [FAIL] — directory not found", true);
        }

        int sets = 0;
        int incomplete = 0;
        foreach (string dir in Directory.GetDirectories(root))
        {
            string name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            sets++;
            if (!MaterialSetInspector.Inspect(root, name, File.Exists).IsComplete)
            {
                incomplete++;
            }
        }

        bool problem = incomplete > 0;
        string status = problem ? "WARN" : "ok";
        return (string.Create(
            CultureInfo.InvariantCulture, $"  material-root \"{path}\" [{status}] — {sets} set(s), {incomplete} incomplete"), problem);
    }

    private static string ResolveBaseDirectory(EditorArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.ContentRoot))
        {
            return args.ContentRoot;
        }

        return Path.GetDirectoryName(Path.GetFullPath(args.ScenePath!)) ?? ".";
    }
}
