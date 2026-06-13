using System.IO;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Opus.Content.Sample;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorModelRunnerTests
{
    [Fact]
    public void Inspect_prints_a_summary_for_the_sample_model()
    {
        using var temp = new TempDirectory();
        var path = temp.File("tank.glb");
        File.WriteAllBytes(path, SampleAlphaTankGltfWriter.BuildGlb());
        var log = new CapturingLog();

        var code = EditorModelRunner.RunInspect(new EditorArgs(EditorMode.Inspect, path, null, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("alpha-sample-tank-marker");
    }

    [Fact]
    public void Inspect_missing_file_returns_io_failed()
    {
        using var temp = new TempDirectory();

        var code = EditorModelRunner.RunInspect(
            new EditorArgs(EditorMode.Inspect, temp.File("missing.glb"), null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitIoFailed);
    }

    [Fact]
    public void Inspect_garbage_file_returns_io_failed()
    {
        using var temp = new TempDirectory();
        var path = temp.File("broken.glb");
        File.WriteAllText(path, "not a glb");

        var code = EditorModelRunner.RunInspect(
            new EditorArgs(EditorMode.Inspect, path, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitIoFailed);
    }

    [Fact]
    public void Inspect_without_a_path_returns_usage()
    {
        var code = EditorModelRunner.RunInspect(
            new EditorArgs(EditorMode.Inspect, null, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }
}
