using System.IO;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorMaterialRunnerTests
{
    private static void WriteMap(string root, string material, string token)
    {
        string dir = Path.Combine(root, material);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{material}_{token}.png"), new byte[] { 1 });
    }

    [Fact]
    public void Reports_a_complete_material_set()
    {
        using var temp = new TempDirectory();
        foreach (string token in new[] { "basecolor", "normal", "orm", "emissive" })
        {
            WriteMap(temp.Root, "brick", token);
        }

        var log = new CapturingLog();
        var code = EditorMaterialRunner.RunMaterials(
            new EditorArgs(EditorMode.Materials, temp.Root, null, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("brick");
        log.Joined.Should().Contain("[complete]");
        log.Joined.Should().Contain("1 complete");
    }

    [Fact]
    public void Flags_an_incomplete_material_set()
    {
        using var temp = new TempDirectory();
        WriteMap(temp.Root, "mud", "basecolor");

        var log = new CapturingLog();
        var code = EditorMaterialRunner.RunMaterials(
            new EditorArgs(EditorMode.Materials, temp.Root, null, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("[incomplete]");
        log.Joined.Should().Contain("MISSING");
    }

    [Fact]
    public void Name_filter_limits_to_one_material()
    {
        using var temp = new TempDirectory();
        WriteMap(temp.Root, "brick", "basecolor");
        WriteMap(temp.Root, "stone", "basecolor");

        var log = new CapturingLog();
        var code = EditorMaterialRunner.RunMaterials(
            new EditorArgs(EditorMode.Materials, temp.Root, "stone", string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("stone");
        log.Joined.Should().NotContain("brick");
        log.Joined.Should().Contain("1 material(s)");
    }

    [Fact]
    public void Missing_root_returns_io_failure()
    {
        var log = new CapturingLog();
        var code = EditorMaterialRunner.RunMaterials(
            new EditorArgs(
                EditorMode.Materials,
                Path.Combine(Path.GetTempPath(), "opus-editor-no-such-root-xyz"),
                null,
                string.Empty),
            log);

        code.Should().Be(EditorConsoleRunner.ExitIoFailed);
    }

    [Fact]
    public void No_path_returns_usage()
    {
        var log = new CapturingLog();
        var code = EditorMaterialRunner.RunMaterials(
            new EditorArgs(EditorMode.Materials, null, null, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }
}
