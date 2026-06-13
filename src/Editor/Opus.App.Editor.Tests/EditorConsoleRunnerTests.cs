using System.IO;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorConsoleRunnerTests
{
    [Fact]
    public void New_creates_a_scene_file_and_returns_ok()
    {
        using var temp = new TempDirectory();
        var path = temp.File("h.scene.json");

        var code = EditorConsoleRunner.RunNew(
            new EditorArgs(EditorMode.New, path, "Harbor", string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitOk);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void New_without_a_path_returns_usage()
    {
        var code = EditorConsoleRunner.RunNew(
            new EditorArgs(EditorMode.New, null, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Show_prints_pseudo_code_for_a_created_scene()
    {
        using var temp = new TempDirectory();
        var path = temp.File("h.scene.json");
        EditorConsoleRunner.RunNew(
            new EditorArgs(EditorMode.New, path, "Harbor", string.Empty), new CapturingLog());
        var log = new CapturingLog();

        var code = EditorConsoleRunner.RunShow(
            new EditorArgs(EditorMode.Show, path, null, string.Empty), log, dslOnly: false);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("scene \"Harbor\"");
    }

    [Fact]
    public void Show_missing_file_returns_io_failed()
    {
        using var temp = new TempDirectory();

        var code = EditorConsoleRunner.RunShow(
            new EditorArgs(EditorMode.Show, temp.File("missing.json"), null, string.Empty),
            new CapturingLog(),
            dslOnly: true);

        code.Should().Be(EditorConsoleRunner.ExitIoFailed);
    }
}
