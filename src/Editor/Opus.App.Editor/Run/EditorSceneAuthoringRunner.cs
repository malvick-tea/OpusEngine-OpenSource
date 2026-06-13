using System;
using System.Globalization;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless scene authoring for the editor CLI: place a model into a scene file. Loads the scene, runs the
/// placement through the same <see cref="EditorDocument"/> command path the visual editor will use, saves
/// atomically, and prints the updated pseudo-code mirror — so a map can be assembled from the command line
/// before the viewport exists, and the GUI later reuses this exact authoring core.
/// </summary>
public static class EditorSceneAuthoringRunner
{
    public static int RunPlace(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath) || string.IsNullOrWhiteSpace(args.AssetRef))
        {
            log.Error("place requires a scene path and an asset reference.");
            return EditorConsoleRunner.ExitUsage;
        }

        var loaded = EditorSceneFileStore.Load(args.ScenePath);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        var document = new EditorDocument();
        document.LoadScene(loaded.Unwrap());
        string name = ResolveNodeName(args);
        var transform = new EditorTransform(args.Position ?? Float3.Zero, Float3.Zero, Float3.One);
        var id = document.PlaceNode(name, args.AssetRef, transform);

        var saved = EditorSceneFileStore.Save(args.ScenePath, document.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Placed '{name}' (#{id}) from '{args.AssetRef}' into {args.ScenePath}."));
        log.Info(document.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunRemove(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var opened = OpenWithNode(args, "scene-remove", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        context.Document.RemoveNode(context.Id);
        return SaveAndPrint(args.ScenePath!, context.Document, log, $"Removed node #{context.Id}.");
    }

    public static int RunRename(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.SceneName))
        {
            log.Error("scene-rename requires a new node name.");
            return EditorConsoleRunner.ExitUsage;
        }

        var opened = OpenWithNode(args, "scene-rename", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        context.Document.RenameNode(context.Id, args.SceneName);
        return SaveAndPrint(
            args.ScenePath!, context.Document, log, $"Renamed node #{context.Id} to '{args.SceneName}'.");
    }

    public static int RunMove(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (args.Position is not { } position)
        {
            log.Error("scene-move requires --at x,y,z.");
            return EditorConsoleRunner.ExitUsage;
        }

        var opened = OpenWithNode(args, "scene-move", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        var current = context.Document.Scene.Find(context.Id)!.Transform;
        context.Document.TransformNode(context.Id, current with { Position = position });
        return SaveAndPrint(args.ScenePath!, context.Document, log, $"Moved node #{context.Id} to {position}.");
    }

    public static int RunRotate(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (args.Position is not { } rotation)
        {
            log.Error("scene-rotate requires --euler yaw,pitch,roll.");
            return EditorConsoleRunner.ExitUsage;
        }

        var opened = OpenWithNode(args, "scene-rotate", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        var current = context.Document.Scene.Find(context.Id)!.Transform;
        context.Document.TransformNode(context.Id, current with { RotationEulerDegrees = rotation });
        return SaveAndPrint(
            args.ScenePath!, context.Document, log, $"Rotated node #{context.Id} to {rotation} degrees.");
    }

    public static int RunScale(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (args.Position is not { } scale)
        {
            log.Error("scene-scale requires --scale sx,sy,sz.");
            return EditorConsoleRunner.ExitUsage;
        }

        var opened = OpenWithNode(args, "scene-scale", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        var current = context.Document.Scene.Find(context.Id)!.Transform;
        context.Document.TransformNode(context.Id, current with { Scale = scale });
        return SaveAndPrint(args.ScenePath!, context.Document, log, $"Scaled node #{context.Id} to {scale}.");
    }

    public static int RunDuplicate(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var opened = OpenWithNode(args, "scene-duplicate", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        var id = context.Document.DuplicateNode(context.Id, args.Position);
        string name = context.Document.Scene.Find(id)!.Name;

        return SaveAndPrint(
            args.ScenePath!, context.Document, log, $"Duplicated node #{context.Id} as '{name}' (#{id}).");
    }

    public static int RunParent(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var opened = OpenWithNode(args, "scene-parent", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        if (!int.TryParse(args.SceneName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parentValue))
        {
            log.Error("scene-parent requires a parent node id.");
            return EditorConsoleRunner.ExitUsage;
        }

        var parentId = new SceneNodeId(parentValue);
        if (!context.Document.Scene.Contains(parentId))
        {
            log.Error($"Scene has no node #{parentValue}.");
            return EditorConsoleRunner.ExitUsage;
        }

        if (context.Document.Scene.Find(context.Id)!.ParentId == parentId)
        {
            log.Info($"Node #{context.Id} is already parented under #{parentValue}.");
            log.Info(context.Document.ToPseudoCode());
            return EditorConsoleRunner.ExitOk;
        }

        if (!context.Document.SetNodeParent(context.Id, parentId))
        {
            log.Error(
                $"Cannot parent node #{context.Id} under #{parentValue}: a node cannot parent onto itself or a descendant.");
            return EditorConsoleRunner.ExitUsage;
        }

        return SaveAndPrint(
            args.ScenePath!, context.Document, log, $"Parented node #{context.Id} under #{parentValue}.");
    }

    public static int RunUnparent(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var opened = OpenWithNode(args, "scene-unparent", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        if (context.Document.SetNodeParent(context.Id, null))
        {
            return SaveAndPrint(
                args.ScenePath!, context.Document, log, $"Detached node #{context.Id} to a root.");
        }

        log.Info($"Node #{context.Id} is already a root.");
        log.Info(context.Document.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    private static (EditorDocument Document, SceneNodeId Id)? OpenWithNode(
        EditorArgs args, string verb, ILog log, out int code)
    {
        if (string.IsNullOrWhiteSpace(args.ScenePath) || string.IsNullOrWhiteSpace(args.AssetRef))
        {
            log.Error($"{verb} requires a scene path and a node id.");
            code = EditorConsoleRunner.ExitUsage;
            return null;
        }

        if (!int.TryParse(args.AssetRef, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idValue))
        {
            log.Error($"Invalid node id '{args.AssetRef}'.");
            code = EditorConsoleRunner.ExitUsage;
            return null;
        }

        var loaded = EditorSceneFileStore.Load(args.ScenePath);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            code = EditorConsoleRunner.ExitIoFailed;
            return null;
        }

        var document = new EditorDocument();
        document.LoadScene(loaded.Unwrap());
        var id = new SceneNodeId(idValue);
        if (!document.Scene.Contains(id))
        {
            log.Error($"Scene has no node #{idValue}.");
            code = EditorConsoleRunner.ExitUsage;
            return null;
        }

        code = EditorConsoleRunner.ExitOk;
        return (document, id);
    }

    private static int SaveAndPrint(string path, EditorDocument document, ILog log, string message)
    {
        var saved = EditorSceneFileStore.Save(path, document.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(message);
        log.Info(document.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    private static string ResolveNodeName(EditorArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.SceneName))
        {
            return args.SceneName;
        }

        string stem = Path.GetFileNameWithoutExtension(args.AssetRef!);
        return string.IsNullOrWhiteSpace(stem) ? "node" : stem;
    }
}
