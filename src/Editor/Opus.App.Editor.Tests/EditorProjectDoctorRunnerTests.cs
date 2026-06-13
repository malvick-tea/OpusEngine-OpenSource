using System.IO;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorProjectDoctorRunnerTests
{
    private static void AddRef(string project, string kind, string reference)
    {
        EditorProjectRunner.RunAdd(
            new EditorArgs(EditorMode.ProjectAdd, project, kind, string.Empty, AssetRef: reference), new CapturingLog());
    }

    private static void NewGraph(string path, string state, bool entry)
    {
        EditorAnimationRunner.RunNew(new EditorArgs(EditorMode.AnimNew, path, "G", string.Empty), new CapturingLog());
        EditorAnimationRunner.RunAddState(
            new EditorArgs(EditorMode.AnimState, path, null, string.Empty, Animation: new AnimationArgs(StateName: state, MakeEntry: entry)),
            new CapturingLog());
    }

    [Fact]
    public void Healthy_project_reports_no_problems()
    {
        using var temp = new TempDirectory();
        string root = temp.Root;
        EditorConsoleRunner.RunNew(
            new EditorArgs(EditorMode.New, Path.Combine(root, "harbor.scene.json"), "Harbor", string.Empty), new CapturingLog());
        NewGraph(Path.Combine(root, "loco.animgraph.json"), "Idle", entry: true);
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        Directory.CreateDirectory(Path.Combine(root, "textures", "brick"));
        foreach (string map in new[] { "basecolor", "normal", "orm", "emissive" })
        {
            File.WriteAllBytes(Path.Combine(root, "textures", "brick", $"brick_{map}.png"), new byte[] { 1 });
        }

        string project = Path.Combine(root, "p.project.json");
        EditorProjectRunner.RunNew(new EditorArgs(EditorMode.ProjectNew, project, "Campaign", string.Empty), new CapturingLog());
        AddRef(project, "content-root", "assets");
        AddRef(project, "scene", "harbor.scene.json");
        AddRef(project, "animgraph", "loco.animgraph.json");
        AddRef(project, "material-root", "textures");

        var log = new CapturingLog();
        var code = EditorProjectDoctorRunner.RunDoctor(
            new EditorArgs(EditorMode.ProjectDoctor, project, root, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("0 problem(s)");
    }

    [Fact]
    public void Problems_are_counted_and_described()
    {
        using var temp = new TempDirectory();
        string root = temp.Root;
        NewGraph(Path.Combine(root, "bad.animgraph.json"), "Idle", entry: false);

        string project = Path.Combine(root, "p.project.json");
        EditorProjectRunner.RunNew(new EditorArgs(EditorMode.ProjectNew, project, "Campaign", string.Empty), new CapturingLog());
        AddRef(project, "animgraph", "bad.animgraph.json");
        AddRef(project, "scene", "ghost.scene.json");
        AddRef(project, "content-root", "nope");

        var log = new CapturingLog();
        var code = EditorProjectDoctorRunner.RunDoctor(
            new EditorArgs(EditorMode.ProjectDoctor, project, root, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("WARN");
        log.Joined.Should().Contain("FAIL");
        log.Joined.Should().Contain("3 problem(s)");
    }

    [Fact]
    public void Scene_with_an_unresolvable_asset_is_flagged()
    {
        using var temp = new TempDirectory();
        string root = temp.Root;
        string scene = Path.Combine(root, "harbor.scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, scene, "Harbor", string.Empty), new CapturingLog());
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, scene, null, string.Empty, "models/ghost.glb"), new CapturingLog());
        Directory.CreateDirectory(Path.Combine(root, "assets"));

        string project = Path.Combine(root, "p.project.json");
        EditorProjectRunner.RunNew(new EditorArgs(EditorMode.ProjectNew, project, "Campaign", string.Empty), new CapturingLog());
        AddRef(project, "content-root", "assets");
        AddRef(project, "scene", "harbor.scene.json");

        var log = new CapturingLog();
        var code = EditorProjectDoctorRunner.RunDoctor(
            new EditorArgs(EditorMode.ProjectDoctor, project, root, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("unresolved asset");
        log.Joined.Should().Contain("1 problem(s)");
    }

    [Fact]
    public void Doctor_without_a_path_returns_usage()
    {
        var code = EditorProjectDoctorRunner.RunDoctor(
            new EditorArgs(EditorMode.ProjectDoctor, null, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }
}
