using FluentAssertions;
using Xunit;

namespace Opus.Foundation.Tests.Versioning;

public sealed class BuildInfoTests
{
    [Fact]
    public void Current_returns_the_same_instance_on_every_access()
    {
        var a = BuildInfo.Current;
        var b = BuildInfo.Current;
        b.Should().BeSameAs(a, "BuildInfo is captured once and cached");
    }

    [Fact]
    public void Current_populates_runtime_metadata()
    {
        var info = BuildInfo.Current;

        info.Engine.Should().Be(EngineIdentity.Current);
        info.ProjectName.Should().NotBeNullOrWhiteSpace();
        info.FrameworkDescription.Should().StartWith(".NET");
        info.OperatingSystem.Should().NotBeNullOrWhiteSpace();
        info.ProcessArchitecture.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Banner_line_contains_every_field()
    {
        var banner = BuildInfo.Current.ToBannerLine();

        banner.Should().Contain(EngineIdentity.Current.DisplayName);
        banner.Should().Contain(EngineIdentity.Current.ProductVersion.ToString());
        banner.Should().Contain(BuildInfo.Current.ProjectName);
        banner.Should().Contain(BuildInfo.Current.Version.ToString());
        banner.Should().Contain(BuildInfo.Current.BuildConfiguration);
        banner.Should().Contain(BuildInfo.Current.FrameworkDescription);
        banner.Should().Contain(BuildInfo.Current.OperatingSystem);
        banner.Should().Contain(BuildInfo.Current.ProcessArchitecture);
    }

    [Fact]
    public void Report_lines_are_stable_and_include_identity_and_runtime()
    {
        var lines = BuildInfo.Current.ToReportLines();

        lines.Should().Contain("product: Opus 0.1");
        lines.Should().Contain("productVersion: 0.1.0-alpha");
        lines.Should().Contain("releaseChannel: alpha");
        lines.Should().Contain($"assembly: {BuildInfo.Current.ProjectName}");
        lines.Should().Contain($"assemblyVersion: {BuildInfo.Current.Version}");
        lines.Should().Contain($"configuration: {BuildInfo.Current.BuildConfiguration}");
        lines.Should().Contain($"framework: {BuildInfo.Current.FrameworkDescription}");
        lines.Should().Contain($"os: {BuildInfo.Current.OperatingSystem}");
        lines.Should().Contain($"processArchitecture: {BuildInfo.Current.ProcessArchitecture}");
        lines.Should().Contain("assemblyCompatibility: Opus.*");
    }

    [Fact]
    public void Report_text_joins_report_lines()
    {
        var text = BuildInfo.Current.ToReportText();

        text.Should().Contain("product: Opus 0.1");
        text.Should().Contain("assemblyCompatibility: Opus.*");
    }
}
