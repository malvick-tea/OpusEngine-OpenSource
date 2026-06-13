using System;
using System.Globalization;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Content;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless model inspection for the editor CLI: read a glTF/GLB file and print a summary (meshes /
/// primitives / vertices / triangles / materials / nodes / bounds, plus a per-mesh breakdown). The
/// byte-level inspection is pure in <see cref="ModelInspector"/>; this runner owns the file read and the
/// formatting, returning conventional exit codes.
/// </summary>
public static class EditorModelRunner
{
    public static int RunInspect(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath))
        {
            log.Error("inspect requires a model file path.");
            return EditorConsoleRunner.ExitUsage;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(args.ScenePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            log.Error($"Cannot read model '{args.ScenePath}': {ex.Message}");
            return EditorConsoleRunner.ExitIoFailed;
        }

        var result = ModelInspector.TryInspect(bytes, args.ScenePath);
        if (result.IsErr)
        {
            log.Error(result.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        PrintInspection(log, result.Unwrap());
        return EditorConsoleRunner.ExitOk;
    }

    private static void PrintInspection(ILog log, ModelInspection model)
    {
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Model '{model.AssetPath}': {model.MeshCount} mesh(es), {model.PrimitiveCount} primitive(s), {model.VertexCount} vertices, {model.TriangleCount} triangle(s)."));
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Materials referenced: {model.MaterialReferenceCount}; nodes: {model.NodeCount} ({model.RootNodeCount} root); tangents: {model.HasTangents}; uvs: {model.HasUvs}."));
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Local bounds: min ({model.BoundsMin.X}, {model.BoundsMin.Y}, {model.BoundsMin.Z}) max ({model.BoundsMax.X}, {model.BoundsMax.Y}, {model.BoundsMax.Z})."));
        foreach (var mesh in model.Meshes)
        {
            log.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"  mesh '{mesh.Name}': {mesh.PrimitiveCount} primitive(s), {mesh.VertexCount} vertices, {mesh.TriangleCount} triangle(s)."));
        }
    }
}
