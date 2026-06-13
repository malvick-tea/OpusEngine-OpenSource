using System;
using System.Globalization;
using Opus.App.Editor.Cli;
using Opus.Editor.Content;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless scene content reporting for the editor CLI: load a scene, resolve each referenced asset under
/// a content root, inspect it, and print the developer-facing cost report (assets / instances / estimated
/// geometry, with missing references flagged). The aggregation is pure in <see cref="SceneContentReporter"/>;
/// this runner owns the file resolution and formatting.
/// </summary>
public static class EditorReportRunner
{
    public static int RunReport(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("report requires a scene file path.");
            return EditorConsoleRunner.ExitUsage;
        }

        var loaded = EditorSceneFileStore.Load(args.ScenePath);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        var scene = new EditorScene();
        scene.Load(loaded.Unwrap());
        string root = EditorModelResolver.ResolveContentRoot(args);
        var report = SceneContentReporter.Build(scene, assetRef => EditorModelResolver.InspectUnderRoot(root, assetRef));
        PrintReport(log, report, root);
        return EditorConsoleRunner.ExitOk;
    }

    private static void PrintReport(ILog log, SceneContentReport report, string root)
    {
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Scene content (root {root}): {report.NodeCount} node(s), {report.DistinctAssetCount} asset(s) ({report.ResolvedAssetCount} resolved, {report.MissingAssetCount} missing)."));
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Estimated geometry: {report.TotalVertices} vertices, {report.TotalTriangles} triangles."));
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Lights: {report.Lights.Total} ({report.Lights.Directional} directional, {report.Lights.Point} point, {report.Lights.Spot} spot)."));
        foreach (var usage in report.Assets)
        {
            string status = usage.Resolved ? "ok" : "MISSING";
            log.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"  {usage.AssetRef} x{usage.InstanceCount} [{status}]: {usage.TriangleCount} tri/inst."));
        }
    }
}
