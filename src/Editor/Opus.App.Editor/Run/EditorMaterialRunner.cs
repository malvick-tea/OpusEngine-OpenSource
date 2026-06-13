using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Content;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless PBR material-set validation for the editor CLI: scan a textures root (or a single named
/// material) against the on-disk authoring convention and print which maps are present, which the runtime
/// will substitute with a neutral fallback, and whether each set is fully authored. The completeness logic
/// is pure in <see cref="MaterialSetInspector"/>; this runner owns directory enumeration and formatting.
/// </summary>
public static class EditorMaterialRunner
{
    public static int RunMaterials(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("materials requires a textures root directory.");
            return EditorConsoleRunner.ExitUsage;
        }

        string root = args.ScenePath;
        if (!Directory.Exists(root))
        {
            log.Error($"Textures root not found: {root}");
            return EditorConsoleRunner.ExitIoFailed;
        }

        var names = ResolveMaterialNames(root, args.SceneName);
        if (names.Count == 0)
        {
            log.Info($"No material folders found under {root}.");
            return EditorConsoleRunner.ExitOk;
        }

        int complete = 0;
        foreach (string name in names)
        {
            var report = MaterialSetInspector.Inspect(root, name, File.Exists);
            PrintReport(log, report);
            if (report.IsComplete)
            {
                complete++;
            }
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"{names.Count} material(s): {complete} complete, {names.Count - complete} incomplete."));
        return EditorConsoleRunner.ExitOk;
    }

    private static IReadOnlyList<string> ResolveMaterialNames(string root, string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            return new[] { filter };
        }

        var names = new List<string>();
        foreach (string dir in Directory.GetDirectories(root))
        {
            string? name = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static void PrintReport(ILog log, MaterialSetReport report)
    {
        string status = report.IsComplete ? "complete" : "incomplete";
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Material '{report.MaterialName}': {report.PresentCount}/{MaterialSetConvention.AllKinds.Count} maps [{status}]."));
        foreach (var map in report.Maps)
        {
            string state = map.Present ? "ok" : "MISSING";
            log.Info($"  {MaterialSetConvention.Token(map.Kind)} [{state}]: {map.RelativePath}");
        }
    }
}
