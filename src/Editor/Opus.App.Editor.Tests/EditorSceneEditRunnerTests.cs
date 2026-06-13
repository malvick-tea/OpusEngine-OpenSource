using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorSceneEditRunnerTests
{
    private static string SceneWithOneNode(TempDirectory temp)
    {
        string path = temp.File("h.scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, path, "Harbor", string.Empty), new CapturingLog());
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, "alpha", string.Empty, "models/tank.glb"), new CapturingLog());
        return path;
    }

    [Fact]
    public void Remove_drops_the_node()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);
        var log = new CapturingLog();

        var code = EditorSceneAuthoringRunner.RunRemove(
            new EditorArgs(EditorMode.SceneRemove, path, null, string.Empty, AssetRef: "1"), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Removed node #1");

        var show = new CapturingLog();
        EditorConsoleRunner.RunShow(new EditorArgs(EditorMode.Show, path, null, string.Empty), show, dslOnly: false);
        show.Joined.Should().Contain("0 node(s)");
    }

    [Fact]
    public void Rename_changes_the_node_name()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);
        var log = new CapturingLog();

        var code = EditorSceneAuthoringRunner.RunRename(
            new EditorArgs(EditorMode.SceneRename, path, "bravo", string.Empty, AssetRef: "1"), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("node \"bravo\"");
    }

    [Fact]
    public void Move_updates_the_position()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);
        var log = new CapturingLog();

        var code = EditorSceneAuthoringRunner.RunMove(
            new EditorArgs(EditorMode.SceneMove, path, null, string.Empty, AssetRef: "1", Position: new Float3(3f, 0f, 4f)),
            log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("position (3, 0, 4)");
    }

    [Fact]
    public void Rotate_updates_the_euler_rotation()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);
        var log = new CapturingLog();

        var code = EditorSceneAuthoringRunner.RunRotate(
            new EditorArgs(EditorMode.SceneRotate, path, null, string.Empty, AssetRef: "1", Position: new Float3(0f, 90f, 0f)),
            log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("rotation (0, 90, 0)");
    }

    [Fact]
    public void Scale_updates_the_scale()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);
        var log = new CapturingLog();

        var code = EditorSceneAuthoringRunner.RunScale(
            new EditorArgs(EditorMode.SceneScale, path, null, string.Empty, AssetRef: "1", Position: new Float3(2f, 2f, 2f)),
            log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("scale (2, 2, 2)");
    }

    [Fact]
    public void Rotate_preserves_position_and_scale()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);
        EditorSceneAuthoringRunner.RunMove(
            new EditorArgs(EditorMode.SceneMove, path, null, string.Empty, AssetRef: "1", Position: new Float3(5f, 0f, 0f)),
            new CapturingLog());

        EditorSceneAuthoringRunner.RunRotate(
            new EditorArgs(EditorMode.SceneRotate, path, null, string.Empty, AssetRef: "1", Position: new Float3(0f, 45f, 0f)),
            new CapturingLog());

        var loaded = EditorSceneFileStore.Load(path).Unwrap();
        var node = loaded.Nodes[0];
        node.Transform.Position.Should().Be(new Float3(5f, 0f, 0f), "rotate must not disturb position");
        node.Transform.RotationEulerDegrees.Should().Be(new Float3(0f, 45f, 0f));
        node.Transform.Scale.Should().Be(Float3.One);
    }

    [Fact]
    public void Rotate_without_an_euler_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);

        var code = EditorSceneAuthoringRunner.RunRotate(
            new EditorArgs(EditorMode.SceneRotate, path, null, string.Empty, AssetRef: "1"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Scale_without_a_scale_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);

        var code = EditorSceneAuthoringRunner.RunScale(
            new EditorArgs(EditorMode.SceneScale, path, null, string.Empty, AssetRef: "1"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Remove_of_a_missing_node_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);

        var code = EditorSceneAuthoringRunner.RunRemove(
            new EditorArgs(EditorMode.SceneRemove, path, null, string.Empty, AssetRef: "99"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Invalid_node_id_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);

        var code = EditorSceneAuthoringRunner.RunRemove(
            new EditorArgs(EditorMode.SceneRemove, path, null, string.Empty, AssetRef: "notanumber"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Move_without_a_position_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = SceneWithOneNode(temp);

        var code = EditorSceneAuthoringRunner.RunMove(
            new EditorArgs(EditorMode.SceneMove, path, null, string.Empty, AssetRef: "1"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Parent_nests_the_child_under_the_parent()
    {
        using var temp = new TempDirectory();
        string path = SceneWithTwoNodes(temp);
        var log = new CapturingLog();

        // Child id rides AssetRef, parent id rides SceneName (the parser's positional layout).
        var code = EditorSceneAuthoringRunner.RunParent(
            new EditorArgs(EditorMode.SceneParent, path, "1", string.Empty, AssetRef: "2"), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Parented node #2 under #1");
        // The mirror nests the box block inside the group block.
        log.Joined.Should().Contain("node \"group\" {");
        log.Joined.Should().Contain("    node \"box\" {");
    }

    [Fact]
    public void Parent_onto_a_descendant_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = SceneWithTwoNodes(temp);
        EditorSceneAuthoringRunner.RunParent(
            new EditorArgs(EditorMode.SceneParent, path, "1", string.Empty, AssetRef: "2"), new CapturingLog());

        var log = new CapturingLog();
        var code = EditorSceneAuthoringRunner.RunParent(
            new EditorArgs(EditorMode.SceneParent, path, "2", string.Empty, AssetRef: "1"), log);

        code.Should().Be(EditorConsoleRunner.ExitUsage);
        log.Joined.Should().Contain("cannot parent onto itself or a descendant");
    }

    [Fact]
    public void Parent_onto_a_missing_parent_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = SceneWithTwoNodes(temp);

        var code = EditorSceneAuthoringRunner.RunParent(
            new EditorArgs(EditorMode.SceneParent, path, "99", string.Empty, AssetRef: "2"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Unparent_detaches_the_child_back_to_a_root()
    {
        using var temp = new TempDirectory();
        string path = SceneWithTwoNodes(temp);
        EditorSceneAuthoringRunner.RunParent(
            new EditorArgs(EditorMode.SceneParent, path, "1", string.Empty, AssetRef: "2"), new CapturingLog());

        var log = new CapturingLog();
        var code = EditorSceneAuthoringRunner.RunUnparent(
            new EditorArgs(EditorMode.SceneUnparent, path, null, string.Empty, AssetRef: "2"), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Detached node #2 to a root");
        // Both nodes are roots again: neither is nested (every node block at the same indent).
        log.Joined.Should().NotContain("    node \"box\" {");
    }

    private static string SceneWithTwoNodes(TempDirectory temp)
    {
        string path = temp.File("h.scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, path, "Harbor", string.Empty), new CapturingLog());
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, "group", string.Empty, "models/group.glb"), new CapturingLog());
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, path, "box", string.Empty, "models/box.glb"), new CapturingLog());
        return path;
    }
}
