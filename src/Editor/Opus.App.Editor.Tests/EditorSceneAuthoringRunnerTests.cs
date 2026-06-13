using System.Linq;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorSceneAuthoringRunnerTests
{
    private static string NewScene(TempDirectory temp, string name)
    {
        var path = temp.File(name + ".scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, path, name, string.Empty), new CapturingLog());
        return path;
    }

    [Fact]
    public void Place_adds_a_node_persists_it_and_prints_pseudo_code()
    {
        using var temp = new TempDirectory();
        var path = NewScene(temp, "Harbor");
        var log = new CapturingLog();

        var code = EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, null, string.Empty, "models/tank.glb", new Float3(10f, 0f, 5f)),
            log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        var reloaded = EditorSceneFileStore.Load(path).Unwrap();
        reloaded.Nodes.Should().ContainSingle(n => n.AssetRef == "models/tank.glb");
        log.Joined.Should().Contain("position (10, 0, 5)");
    }

    [Fact]
    public void Place_derives_the_node_name_from_the_asset_stem()
    {
        using var temp = new TempDirectory();
        var path = NewScene(temp, "Harbor");

        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, null, string.Empty, "models/tank.glb"), new CapturingLog());

        EditorSceneFileStore.Load(path).Unwrap().Nodes.Should().ContainSingle(n => n.Name == "tank");
    }

    [Fact]
    public void Place_without_an_asset_returns_usage()
    {
        using var temp = new TempDirectory();
        var path = NewScene(temp, "Harbor");

        var code = EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Place_into_a_missing_scene_is_io_failed()
    {
        using var temp = new TempDirectory();

        var code = EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, temp.File("missing.json"), null, string.Empty, "m.glb"),
            new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitIoFailed);
    }

    [Fact]
    public void Duplicate_clones_the_node_with_a_copy_name_at_an_offset()
    {
        using var temp = new TempDirectory();
        var path = NewScene(temp, "Harbor");
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, "alpha", string.Empty, "models/tank.glb", new Float3(2f, 0f, 0f)),
            new CapturingLog());

        var code = EditorSceneAuthoringRunner.RunDuplicate(
            new EditorArgs(EditorMode.SceneDuplicate, path, null, string.Empty, AssetRef: "1"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitOk);
        var nodes = EditorSceneFileStore.Load(path).Unwrap().Nodes;
        nodes.Should().HaveCount(2);
        var copy = nodes.Should().ContainSingle(n => n.Name == "alpha copy").Subject;
        copy.AssetRef.Should().Be("models/tank.glb");
        copy.Transform.Position.X.Should().BeGreaterThan(2f, "the copy is offset from the source");
    }

    [Fact]
    public void Duplicate_honours_an_explicit_position()
    {
        using var temp = new TempDirectory();
        var path = NewScene(temp, "Harbor");
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, "alpha", string.Empty, "m.glb"), new CapturingLog());

        EditorSceneAuthoringRunner.RunDuplicate(
            new EditorArgs(EditorMode.SceneDuplicate, path, null, string.Empty, AssetRef: "1", Position: new Float3(7f, 1f, 3f)),
            new CapturingLog());

        var copy = EditorSceneFileStore.Load(path).Unwrap().Nodes.Single(n => n.Name == "alpha copy");
        copy.Transform.Position.Should().Be(new Float3(7f, 1f, 3f));
    }

    [Fact]
    public void Duplicate_of_a_missing_node_returns_usage()
    {
        using var temp = new TempDirectory();
        var path = NewScene(temp, "Harbor");

        var code = EditorSceneAuthoringRunner.RunDuplicate(
            new EditorArgs(EditorMode.SceneDuplicate, path, null, string.Empty, AssetRef: "99"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }
}
