using System.IO;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorProjectRunnerTests
{
    private static string NewProject(TempDirectory temp)
    {
        string path = temp.File("campaign.project.json");
        EditorProjectRunner.RunNew(
            new EditorArgs(EditorMode.ProjectNew, path, "Campaign", string.Empty), new CapturingLog());
        return path;
    }

    private static void Add(string path, string kind, string reference)
    {
        EditorProjectRunner.RunAdd(
            new EditorArgs(EditorMode.ProjectAdd, path, kind, string.Empty, AssetRef: reference), new CapturingLog());
    }

    [Fact]
    public void New_then_show_round_trips()
    {
        using var temp = new TempDirectory();
        string path = NewProject(temp);
        var log = new CapturingLog();

        var code = EditorProjectRunner.RunShow(
            new EditorArgs(EditorMode.ProjectShow, path, null, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Campaign");
        log.Joined.Should().Contain("project \"Campaign\"");
    }

    [Fact]
    public void Add_scene_appears_in_pseudo_code()
    {
        using var temp = new TempDirectory();
        string path = NewProject(temp);
        var log = new CapturingLog();

        var code = EditorProjectRunner.RunAdd(
            new EditorArgs(EditorMode.ProjectAdd, path, "scene", string.Empty, AssetRef: "harbor.scene.json"), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("scene \"harbor.scene.json\"");
    }

    [Fact]
    public void Add_unknown_kind_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewProject(temp);
        var log = new CapturingLog();

        var code = EditorProjectRunner.RunAdd(
            new EditorArgs(EditorMode.ProjectAdd, path, "widget", string.Empty, AssetRef: "x"), log);

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Check_reports_a_missing_reference()
    {
        using var temp = new TempDirectory();
        string path = NewProject(temp);
        Add(path, "scene", "ghost.scene.json");
        var log = new CapturingLog();

        var code = EditorProjectRunner.RunCheck(
            new EditorArgs(EditorMode.ProjectCheck, path, null, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Scene");
        log.Joined.Should().Contain("ghost.scene.json");
    }

    [Fact]
    public void Check_passes_when_references_resolve()
    {
        using var temp = new TempDirectory();
        string path = NewProject(temp);
        File.WriteAllText(Path.Combine(temp.Root, "real.scene.json"), "{}");
        Directory.CreateDirectory(Path.Combine(temp.Root, "assets"));
        Add(path, "scene", "real.scene.json");
        Add(path, "content-root", "assets");
        var log = new CapturingLog();

        var code = EditorProjectRunner.RunCheck(
            new EditorArgs(EditorMode.ProjectCheck, path, temp.Root, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("all references resolve");
    }

    [Fact]
    public void New_without_a_path_returns_usage()
    {
        var code = EditorProjectRunner.RunNew(
            new EditorArgs(EditorMode.ProjectNew, null, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }
}
