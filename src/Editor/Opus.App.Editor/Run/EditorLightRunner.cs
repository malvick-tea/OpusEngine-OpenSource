using System;
using System.Globalization;
using Opus.App.Editor.Cli;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Headless light authoring for the editor CLI: add, remove, or retune a scene light. Loads the scene, runs
/// the mutation through the same <see cref="EditorDocument"/> command path the visual editor will use, saves
/// atomically, and prints the updated pseudo-code mirror — so a scene can be lit from the command line, and
/// the GUI later reuses this exact authoring core.
/// </summary>
public static class EditorLightRunner
{
    public static int RunAdd(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.ScenePath) || args.Light?.Kind is not { } kind)
        {
            log.Error("light-add requires a scene path and a light kind.");
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
        string name = ResolveLightName(args, kind);
        var id = document.AddLight(ApplyOverrides(CreateBase(kind, name), args.Light!, args.Position));

        var saved = EditorSceneFileStore.Save(args.ScenePath, document.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return EditorConsoleRunner.ExitIoFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture, $"Added {kind} light '{name}' (#{id}) to {args.ScenePath}."));
        log.Info(document.ToPseudoCode());
        return EditorConsoleRunner.ExitOk;
    }

    public static int RunRemove(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var opened = OpenWithLight(args, "light-remove", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        context.Document.RemoveLight(context.Id);
        return SaveAndPrint(
            args.ScenePath!, context.Document, log, $"Removed light #{context.Id} from {args.ScenePath}.");
    }

    public static int RunEdit(EditorArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (args.Light is null)
        {
            log.Error("light-edit requires a scene path and a light id.");
            return EditorConsoleRunner.ExitUsage;
        }

        var opened = OpenWithLight(args, "light-edit", log, out int code);
        if (opened is not { } context)
        {
            return code;
        }

        var existing = context.Document.Scene.FindLight(context.Id)!;
        if (!string.IsNullOrWhiteSpace(args.SceneName))
        {
            existing = existing.WithName(args.SceneName);
        }

        context.Document.SetLight(ApplyOverrides(existing, args.Light, args.Position));
        return SaveAndPrint(args.ScenePath!, context.Document, log, $"Edited light #{context.Id} in {args.ScenePath}.");
    }

    private static (EditorDocument Document, SceneLightId Id)? OpenWithLight(
        EditorArgs args, string verb, ILog log, out int code)
    {
        if (string.IsNullOrWhiteSpace(args.ScenePath) || string.IsNullOrWhiteSpace(args.AssetRef))
        {
            log.Error($"{verb} requires a scene path and a light id.");
            code = EditorConsoleRunner.ExitUsage;
            return null;
        }

        if (!int.TryParse(args.AssetRef, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idValue))
        {
            log.Error($"Invalid light id '{args.AssetRef}'.");
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
        var id = new SceneLightId(idValue);
        if (!document.Scene.ContainsLight(id))
        {
            log.Error($"Scene has no light #{idValue}.");
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

    private static SceneLight CreateBase(SceneLightKind kind, string name) => kind switch
    {
        SceneLightKind.Point => SceneLight.CreatePoint(name),
        SceneLightKind.Spot => SceneLight.CreateSpot(name),
        _ => SceneLight.CreateDirectional(name),
    };

    private static SceneLight ApplyOverrides(SceneLight light, LightArgs spec, Float3? position)
    {
        if (spec.Color is { } color)
        {
            light = light with { Color = color };
        }

        if (spec.Intensity is { } intensity)
        {
            light = light with { Intensity = intensity };
        }

        if (position is { } at)
        {
            light = light with { Position = at };
        }

        if (spec.Direction is { } direction)
        {
            light = light with { Direction = direction };
        }

        if (spec.Range is { } range)
        {
            light = light with { Range = range };
        }

        if (spec.ConeInnerAngleDegrees is { } inner)
        {
            light = light with { SpotInnerAngleDegrees = inner };
        }

        if (spec.ConeOuterAngleDegrees is { } outer)
        {
            light = light with { SpotOuterAngleDegrees = outer };
        }

        return light;
    }

    private static string ResolveLightName(EditorArgs args, SceneLightKind kind)
    {
        if (!string.IsNullOrWhiteSpace(args.SceneName))
        {
            return args.SceneName;
        }

        return kind switch
        {
            SceneLightKind.Point => "point",
            SceneLightKind.Spot => "spot",
            _ => "directional",
        };
    }
}
