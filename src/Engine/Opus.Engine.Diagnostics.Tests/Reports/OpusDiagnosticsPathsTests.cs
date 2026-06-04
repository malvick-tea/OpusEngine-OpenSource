using System;
using System.IO;
using FluentAssertions;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class OpusDiagnosticsPathsTests
{
    [Fact]
    public void Default_root_directory_contains_product_name_and_diagnostics_segment()
    {
        var root = OpusDiagnosticsPaths.DefaultRootDirectory();

        root.Should().NotBeNullOrWhiteSpace();
        root.Should().Contain(EngineIdentity.Current.ProductName);
        root.Should().EndWith(OpusDiagnosticsPaths.DiagnosticsDirectoryName);
        Path.IsPathFullyQualified(root).Should().BeTrue();
    }

    [Fact]
    public void Default_logs_and_reports_directories_are_inside_default_root()
    {
        var root = OpusDiagnosticsPaths.DefaultRootDirectory();

        OpusDiagnosticsPaths.DefaultLogsDirectory().Should().StartWith(root);
        OpusDiagnosticsPaths.DefaultLogsDirectory()
            .Should().EndWith(OpusDiagnosticsPaths.LogsDirectoryName);
        OpusDiagnosticsPaths.DefaultReportsDirectory().Should().StartWith(root);
        OpusDiagnosticsPaths.DefaultReportsDirectory()
            .Should().EndWith(OpusDiagnosticsPaths.ReportsDirectoryName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LogsDirectory_rejects_empty_root(string root)
    {
        var act = () => OpusDiagnosticsPaths.LogsDirectory(root);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportsDirectory_rejects_empty_root(string root)
    {
        var act = () => OpusDiagnosticsPaths.ReportsDirectory(root);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LogsDirectory_resolves_traversal_segments()
    {
        var root = Path.Combine(Path.GetTempPath(), "opus-paths-test", "sub", "..", "child");

        var logs = OpusDiagnosticsPaths.LogsDirectory(root);

        logs.Should().NotContain("..");
        Path.IsPathFullyQualified(logs).Should().BeTrue();
        logs.Should().EndWith(OpusDiagnosticsPaths.LogsDirectoryName);
    }

    [Fact]
    public void Default_smoke_directory_is_inside_default_root_and_named_smoke()
    {
        var root = OpusDiagnosticsPaths.DefaultRootDirectory();

        var smoke = OpusDiagnosticsPaths.DefaultSmokeDirectory();
        smoke.Should().StartWith(root);
        smoke.Should().EndWith(OpusDiagnosticsPaths.SmokeDirectoryName);
        OpusDiagnosticsPaths.SmokeDirectoryName.Should().Be("smoke");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SmokeDirectory_rejects_empty_root(string root)
    {
        var act = () => OpusDiagnosticsPaths.SmokeDirectory(root);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SmokeDirectory_resolves_traversal_segments()
    {
        var root = Path.Combine(Path.GetTempPath(), "opus-smoke-test", "sub", "..", "child");

        var smoke = OpusDiagnosticsPaths.SmokeDirectory(root);

        smoke.Should().NotContain("..");
        Path.IsPathFullyQualified(smoke).Should().BeTrue();
        smoke.Should().EndWith(OpusDiagnosticsPaths.SmokeDirectoryName);
    }

    [Fact]
    public void Default_stress_directory_is_inside_default_root_and_named_stress()
    {
        var root = OpusDiagnosticsPaths.DefaultRootDirectory();

        var stress = OpusDiagnosticsPaths.DefaultStressDirectory();
        stress.Should().StartWith(root);
        stress.Should().EndWith(OpusDiagnosticsPaths.StressDirectoryName);
        OpusDiagnosticsPaths.StressDirectoryName.Should().Be("stress");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StressDirectory_rejects_empty_root(string root)
    {
        var act = () => OpusDiagnosticsPaths.StressDirectory(root);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StressDirectory_resolves_traversal_segments()
    {
        var root = Path.Combine(Path.GetTempPath(), "opus-stress-test", "sub", "..", "child");

        var stress = OpusDiagnosticsPaths.StressDirectory(root);

        stress.Should().NotContain("..");
        Path.IsPathFullyQualified(stress).Should().BeTrue();
        stress.Should().EndWith(OpusDiagnosticsPaths.StressDirectoryName);
    }

    [Fact]
    public void Default_known_issues_directory_is_inside_default_root()
    {
        var root = OpusDiagnosticsPaths.DefaultRootDirectory();

        var ki = OpusDiagnosticsPaths.DefaultKnownIssuesDirectory();
        ki.Should().StartWith(root);
        ki.Should().EndWith(OpusDiagnosticsPaths.KnownIssuesDirectoryName);
        OpusDiagnosticsPaths.KnownIssuesDirectoryName.Should().Be("known-issues");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void KnownIssuesDirectory_rejects_empty_root(string root)
    {
        var act = () => OpusDiagnosticsPaths.KnownIssuesDirectory(root);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Default_screenshots_directory_is_inside_default_root_and_named_screenshots()
    {
        var root = OpusDiagnosticsPaths.DefaultRootDirectory();

        var screenshots = OpusDiagnosticsPaths.DefaultScreenshotsDirectory();
        screenshots.Should().StartWith(root);
        screenshots.Should().EndWith(OpusDiagnosticsPaths.ScreenshotsDirectoryName);
        OpusDiagnosticsPaths.ScreenshotsDirectoryName.Should().Be("screenshots");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ScreenshotsDirectory_rejects_empty_root(string root)
    {
        var act = () => OpusDiagnosticsPaths.ScreenshotsDirectory(root);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveRootDirectory_uses_configured_root_directly_when_usable()
    {
        var configured = Path.Combine(Path.GetTempPath(), "opus-configured-root");

        var root = OpusDiagnosticsPaths.ResolveRootDirectory(configured);

        Path.IsPathFullyQualified(root).Should().BeTrue();
        root.Should().Be(Path.GetFullPath(configured));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveRootDirectory_falls_back_to_per_user_default_when_unset(string? configured)
    {
        var root = OpusDiagnosticsPaths.ResolveRootDirectory(configured);

        root.Should().Contain(EngineIdentity.Current.ProductName);
        root.Should().EndWith(OpusDiagnosticsPaths.DiagnosticsDirectoryName);
        Path.IsPathFullyQualified(root).Should().BeTrue();
    }

    [Fact]
    public void ResolveRootDirectory_resolves_traversal_in_configured_root()
    {
        var configured = Path.Combine(Path.GetTempPath(), "opus-cfg", "sub", "..", "child");

        var root = OpusDiagnosticsPaths.ResolveRootDirectory(configured);

        root.Should().NotContain("..");
        Path.IsPathFullyQualified(root).Should().BeTrue();
        root.Should().EndWith("child");
    }

    [Fact]
    public void ResolveRootDirectory_falls_back_when_configured_root_is_malformed()
    {
        var root = OpusDiagnosticsPaths.ResolveRootDirectory("bad\0path");

        root.Should().Contain(EngineIdentity.Current.ProductName);
        root.Should().EndWith(OpusDiagnosticsPaths.DiagnosticsDirectoryName);
    }

    [Fact]
    public void Default_root_directory_delegates_to_the_environment_variable_value()
    {
        OpusDiagnosticsPaths.RootDirectoryEnvironmentVariable.Should().Be("OPUS_DIAGNOSTICS_DIR");

        OpusDiagnosticsPaths.DefaultRootDirectory()
            .Should().Be(OpusDiagnosticsPaths.ResolveRootDirectory(
                Environment.GetEnvironmentVariable(OpusDiagnosticsPaths.RootDirectoryEnvironmentVariable)));
    }
}
