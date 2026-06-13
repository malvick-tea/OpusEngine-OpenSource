using System;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Opus.Foundation;

namespace Opus.App.Editor;

/// <summary>
/// Entry point for the Opus 0.1 UI Edition editor. The headless console commands (new / show / dsl / place /
/// inspect / materials / anim / project) drive the authoring core, JSON serialisation, and the pseudo-code
/// mirror without a GPU; the <c>window</c> command opens the live D3D12 authoring viewport over the same
/// document cores.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parsed = EditorCliParser.Parse(args);
        var log = new ConsoleLog(LogLevel.Information);
        return Dispatch(parsed, log);
    }

    private static int Dispatch(EditorArgs args, ILog log)
    {
        return args.Mode switch
        {
            EditorMode.New => EditorConsoleRunner.RunNew(args, log),
            EditorMode.Show => EditorConsoleRunner.RunShow(args, log, dslOnly: false),
            EditorMode.Dsl => EditorConsoleRunner.RunShow(args, log, dslOnly: true),
            EditorMode.Inspect => EditorModelRunner.RunInspect(args, log),
            EditorMode.Place => EditorSceneAuthoringRunner.RunPlace(args, log),
            EditorMode.SceneRemove => EditorSceneAuthoringRunner.RunRemove(args, log),
            EditorMode.SceneRename => EditorSceneAuthoringRunner.RunRename(args, log),
            EditorMode.SceneMove => EditorSceneAuthoringRunner.RunMove(args, log),
            EditorMode.SceneRotate => EditorSceneAuthoringRunner.RunRotate(args, log),
            EditorMode.SceneScale => EditorSceneAuthoringRunner.RunScale(args, log),
            EditorMode.SceneDuplicate => EditorSceneAuthoringRunner.RunDuplicate(args, log),
            EditorMode.SceneParent => EditorSceneAuthoringRunner.RunParent(args, log),
            EditorMode.SceneUnparent => EditorSceneAuthoringRunner.RunUnparent(args, log),
            EditorMode.LightAdd => EditorLightRunner.RunAdd(args, log),
            EditorMode.LightRemove => EditorLightRunner.RunRemove(args, log),
            EditorMode.LightEdit => EditorLightRunner.RunEdit(args, log),
            EditorMode.Report => EditorReportRunner.RunReport(args, log),
            EditorMode.Materials => EditorMaterialRunner.RunMaterials(args, log),
            EditorMode.AnimNew => EditorAnimationRunner.RunNew(args, log),
            EditorMode.AnimShow => EditorAnimationRunner.RunShow(args, log),
            EditorMode.AnimState => EditorAnimationRunner.RunAddState(args, log),
            EditorMode.AnimTransition => EditorAnimationRunner.RunAddTransition(args, log),
            EditorMode.AnimRemoveState => EditorAnimationRunner.RunRemoveState(args, log),
            EditorMode.AnimRemoveTransition => EditorAnimationRunner.RunRemoveTransition(args, log),
            EditorMode.ProjectNew => EditorProjectRunner.RunNew(args, log),
            EditorMode.ProjectShow => EditorProjectRunner.RunShow(args, log),
            EditorMode.ProjectAdd => EditorProjectRunner.RunAdd(args, log),
            EditorMode.ProjectCheck => EditorProjectRunner.RunCheck(args, log),
            EditorMode.ProjectDoctor => EditorProjectDoctorRunner.RunDoctor(args, log),
            EditorMode.Window => EditorWindowRunner.RunWindow(args, log, args.WindowMaxFrames),
            _ => PrintHelp(args.HelpReason),
        };
    }

    private static int PrintHelp(string reason)
    {
        Console.Out.Write(EditorCliHelp.Render(reason));
        return string.IsNullOrWhiteSpace(reason) ? 0 : 1;
    }
}
